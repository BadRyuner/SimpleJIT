using Iced.Intel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SimpleJIT;
internal static class Optimizer
{
	internal static byte[] Optimize(Assembler asm, StackInfo stack)
	{
		using (var mem = new MemoryStream())
		{
			var codeWriter = new StreamCodeWriter(mem);
			IList<Instruction> instructions = asm.Instructions as IList<Instruction>;

			InlineMovConstant(instructions);
			InlineAddMovToLea(instructions);
			InlineMovMathToSingleMath(instructions);
			RemoveDebugLocals(instructions);
			AnotherTryToReduceMovs(instructions);
			TryReduceUsedRegisters(instructions);
			RemoveNonSmartTricks(instructions);
			//AvxSSETricks(instructions); // not effective
			MovStackToFreeRegs(instructions, stack);
			BuildRegShield(instructions, stack);

			asm.Assemble(codeWriter, 0);
			return mem.ToArray();
		}
	}

	private static void InlineMovConstant(IList<Instruction> instructions)
	{
		bool can = false;
		Register replaceit = default;
		ulong value = default;
		int pos = default;

		for(int i = 0; i < instructions.Count; i++)
		{
			var instruction = instructions[i];
			if (instruction.Code == Code.Mov_r64_imm64)
			{
				replaceit = instruction.Op0Register;
				value = instruction.Immediate64;
				can = true;
				pos = i;
				continue;
			}
			if (can && instruction.Op1Register == replaceit)
			{
				if (instruction.MemoryDisplacement64 > 0) goto badtry;

				switch(instruction.Code)
				{
					case Code.Add_rm64_r64:
						if (value < 255)
						{
							instruction.Code = Code.Add_rm64_imm8;
							instruction.Op1Kind = OpKind.Immediate8to64;
							instruction.SetImmediate(1, value);
						}
						else
						{
							instruction.Code = Code.Add_rm64_imm32;
							instruction.Op1Kind = OpKind.Immediate32to64;
							instruction.SetImmediate(1, value);
						}
						instructions.RemoveAt(pos);
						i--;
						
						break;
					case Code.Mov_rm64_r64:
						instruction.Code = Code.Mov_rm64_imm32;
						instruction.Op1Kind = OpKind.Immediate32to64;
						instruction.SetImmediate(1, value);
						instructions.RemoveAt(pos);
						i--;
						break;
					default:
						Console.WriteLine($"### Cant inline set {value} in register {replaceit} for {instruction}");
						break;
				}
				instructions[i] = instruction;
				badtry:
				can = false;
			}
		}
	}

	private static void InlineAddMovToLea(IList<Instruction> instructions)
	{
		for (int i = 0; i < instructions.Count - 2; i++)
		{
			var operation = instructions[i];
			var mov = instructions[i+1];

			//if (operation.Code == Code.Mov_rm64_r64)

			if (mov.Code != Code.Mov_rm64_r64) continue;
			if (operation.Op0Register != mov.Op1Register) continue;
			if (mov.Op0Kind == OpKind.Memory) continue;
			if (operation.Code == Code.Add_rm64_imm8 || operation.Code == Code.Add_rm64_imm32
				|| operation.Code == Code.Sub_rm64_imm8 || operation.Code == Code.Sub_rm64_imm32) // mov reg, byte || mov reg, int
			{
				if (mov.Op0Kind == OpKind.Memory) continue; // unsupported

				operation.Code = Code.Lea_r64_m;
				Register ToReg = operation.Op0Register;
				ulong toAdd = operation.GetImmediate(1);
				operation.Op0Register = mov.Op0Register;
				operation.Op1Kind = OpKind.Memory;
				operation.MemoryBase = ToReg;
				if (operation.Code == Code.Sub_rm64_imm8 || operation.Code == Code.Sub_rm64_imm32)
					operation.MemoryDisplacement64 = (ulong)((long)toAdd * -1);
				else
					operation.MemoryDisplacement64 = toAdd;
				operation.MemoryDisplSize = 1;

				instructions[i] = operation;
				instructions.RemoveAt(i+1);
			}
			else if (operation.Code == Code.Add_rm64_r64) // mov reg, reg
			{
				operation.Code = Code.Lea_r64_m;
				Register ToReg = operation.Op0Register;
				Register toAdd = operation.Op1Register;
				operation.Op0Register = mov.Op0Register;
				operation.Op1Kind = OpKind.Memory;
				operation.MemoryBase = ToReg;
				operation.MemoryIndex = toAdd;
				operation.MemoryDisplSize = 1;

				instructions[i] = operation;
				instructions.RemoveAt(i + 1);
			}
		}
	}

	private static void InlineMovMathToSingleMath(IList<Instruction> instructions)
	{
		for (int i = 0; i < instructions.Count - 2; i++)
		{
			var mov = instructions[i];
			var operation = instructions[i + 1];
			if (mov.Mnemonic == Mnemonic.Mov && operation.Mnemonic != Mnemonic.Mov // +_+
				&& mov.Op0Register == operation.Op1Register && mov.Op0Kind != OpKind.Memory && mov.Op1Kind == OpKind.Memory)
			{
				if (operation.Code == Code.Add_rm64_r64)
					operation.Code = Code.Add_r64_rm64;
				else
				{
					Console.WriteLine($"InlineMovMathToSingleMath: Not Implemented for {operation.Code}! Skip...");
					continue;
				}
				operation.Op1Kind = mov.Op1Kind;
				operation.Op1Register = mov.Op1Register;
				operation.MemoryBase = mov.MemoryBase;
				operation.MemoryDisplacement64 = mov.MemoryDisplacement64;
				operation.MemoryDisplSize = mov.MemoryDisplSize;

				instructions[i+1] = operation;
				instructions.RemoveAt(i);
			}
		}
	}

	private static void AnotherTryToReduceMovs(IList<Instruction> instructions)
	{
		for(int i = 0; i < instructions.Count - 2; i += 2)
		{
			var movone = instructions[i];
			var movtwo = instructions[i+1];
			if (movone.Mnemonic != Mnemonic.Mov && movtwo.Mnemonic != Mnemonic.Mov) continue;
			if (movone.Op0Kind == OpKind.Memory) continue;
			if (movone.Op0Register == movtwo.Op1Register)
			{
				movone.Op0Kind = movtwo.Op0Kind;
				movone.Op0Register = movtwo.Op0Register;
				instructions[i] = movone;
			}
		}
	}

	private static void TryReduceUsedRegisters(IList<Instruction> instructions, int step = 0)
	{
		if (step == 2) return;

		Register replacethis = Register.None;
		Register withthis = Register.None;

		for (int i = instructions.Count - 1; i >= 0; i--)
		{
			var instr = instructions[i];
			if (instr.Mnemonic == Mnemonic.Mov)
			{
				if (instr.Op0Kind == OpKind.Register && instr.Op1Kind == OpKind.Register)
				{
					if (replacethis == Register.None)
					{
						replacethis = instr.Op1Register;
						withthis = instr.Op0Register;
						instr.Op1Register = instr.Op0Register;
						instructions[i] = instr;
						continue;
					}
					else if (instr.Op0Register == withthis)
					{
						replacethis = Register.None;
						withthis = Register.None;
						continue;
					}
				}
			}
			if (replacethis == Register.None) continue;
			if (instr.Op0Register == replacethis) instr.Op0Register = withthis;
			if (instr.Op1Register == replacethis) instr.Op1Register = withthis;
			if (instr.Op2Register == replacethis) instr.Op2Register = withthis;
			if (instr.Op3Register == replacethis) instr.Op3Register = withthis;
			if (instr.MemoryBase == replacethis) instr.MemoryBase = withthis;
			//if (instr.Op4Register == replacethis) instr.Op4Register = withthis;
			instructions[i] = instr;
		}

		TryReduceUsedRegisters(instructions, step + 1);
	}

	private static void RemoveNonSmartTricks(IList<Instruction> instructions)
	{
		for(int i = 0; i < instructions.Count; i++)
		{
			var instr = instructions[i];
			if (instr.Code == Code.Mov_rm64_r64 && instr.Op0Register == instr.Op1Register)
			{
				instructions.RemoveAt(i);
				i--;
			}
		}
	}

	private static void AvxSSETricks(IList<Instruction> instructions)
	{
		// math math to SIMD
		for(int i = 0; i < instructions.Count - 4; i++)
		{
			var math1 = instructions[i];
			var math2 = instructions[i+1];
			var math3 = instructions[i+2];
			var math4 = instructions[i+3];

			// for "+" only
			if (math1.Op0Register == math2.Op0Register && math1.Op0Register == math3.Op0Register && math1.Op0Register == math4.Op0Register &&
				math1.Code == math2.Code && math1.Code == math3.Code && math1.Code == math4.Code &&
				math1.MemoryDisplacement64 == math2.MemoryDisplacement64 - 8 && math1.MemoryDisplacement64 == math3.MemoryDisplacement64 - 16 && math1.MemoryDisplacement64 == math4.MemoryDisplacement64 - 24)
			{
				int offset = 4;

				var addto = math1.Op0Register;
				var at = math1.MemoryDisplacement64;

				math1.Code = Code.VEX_Vmovdqu_xmm_xmmm128;
				math1.Op0Register = Register.XMM9;
				
				math2.Code = Code.VEX_Vpaddq_xmm_xmm_xmmm128;
				math2.Op0Register = Register.XMM9;
				math2.Op1Kind = OpKind.Register;
				math2.Op1Register = Register.XMM9;
				math2.Op2Kind = OpKind.Memory;
				math2.MemoryDisplacement64 = at + 16;
				/*
				while (true)
				{
					if (i+offset+2 >= instructions.Count) break;
					var _m1 = instructions[i + offset];
					var _m2 = instructions[i + offset + 1];
					var _m3 = instructions[i + offset + 2];
					var _m4 = instructions[i + offset + 3];
					if (_m1.Code == _m2.Code && _m1.Code == _m3.Code && _m1.Code == _m4.Code && _m4.MemoryDisplacement64 - _m1.MemoryDisplacement64 == 24)
					{
						instructions.RemoveAt(i + offset); // _m1
						instructions.RemoveAt(i + offset); // _m2
						instructions.RemoveAt(i + offset); // _m3
						instructions.RemoveAt(i + offset); // _m4
						instructions.Insert(i + offset, Instruction.Create(Code.VEX_Vpaddq_xmm_xmm_xmmm128, Register.XMM9, Register.XMM9, (AssemblerRegisters.rsp + (int)_m1.MemoryDisplacement64).ToMemoryOperand(64)));
						instructions.Insert(i + offset + 1, Instruction.Create(Code.VEX_Vpaddq_xmm_xmm_xmmm128, Register.XMM9, Register.XMM9, (AssemblerRegisters.rsp + (int)_m3.MemoryDisplacement64).ToMemoryOperand(64)));
						offset += 2;
						continue;
					}
					break;
				} */

				math3.Code = Code.VEX_Vpshufd_xmm_xmmm128_imm8;
				math3.Op0Register = Register.XMM10;
				math3.Op1Kind = OpKind.Register;
				math3.Op1Register = Register.XMM9;
				math3.Op2Kind = OpKind.Immediate8;
				math3.SetImmediate(2, 238);

				math4.Code = Code.VEX_Vpaddq_xmm_xmm_xmmm128;
				math4.Op0Register = Register.XMM9;
				math4.Op1Kind = OpKind.Register;
				math4.Op1Register = Register.XMM9;
				math4.Op2Kind = OpKind.Register;
				math4.Op2Register = Register.XMM10;

				
				instructions[i] = math1;
				instructions[i + 1] = math2;
				instructions[i + 2] = math3;
				instructions[i + 3] = math4;
				instructions.Insert(i+offset, Instruction.Create(Code.VEX_Vmovq_rm64_xmm, Register.R15, Register.XMM9));
				instructions.Insert(i+offset+1, Instruction.Create(Code.Add_rm64_r64, addto, Register.R15));
			}
		}
	}

	private static void MovStackToFreeRegs(IList<Instruction> instructions, StackInfo stack)
	{
		int i;
		HashSet<Register> UsedRegs = new HashSet<Register>();
		Dictionary<ulong, StackSets> stacks = new Dictionary<ulong, StackSets>();
		for(i = 0; i < instructions.Count; i++)
		{
			var instr = instructions[i];

			UsedRegs.Add(instr.Op0Register);
			UsedRegs.Add(instr.Op1Register);
			UsedRegs.Add(instr.Op2Register);
			UsedRegs.Add(instr.Op3Register);

			if (instr.MemoryBase != Register.RSP) continue;
			if (instr.CpuidFeatures.FirstOrDefault() == CpuidFeature.AVX || 
				instr.CpuidFeatures.FirstOrDefault() == CpuidFeature.AVX2) continue;
			if (instr.Op0Kind == OpKind.Memory && instr.MemoryDisplacement64 > 1)
			{
				if (stacks.ContainsKey(instr.MemoryDisplacement64))
					stacks[instr.MemoryDisplacement64].set.Add(i);
				else
				{
					stacks.Add(instr.MemoryDisplacement64, new StackSets());
					stacks[instr.MemoryDisplacement64].set.Add(i);
				}
			}
			if (instr.Op1Kind == OpKind.Memory && instr.MemoryDisplacement64 > 1)
			{
				if (stacks.ContainsKey(instr.MemoryDisplacement64))
					stacks[instr.MemoryDisplacement64].get.Add(i);
				else
				{
					stacks.Add(instr.MemoryDisplacement64, new StackSets());
					stacks[instr.MemoryDisplacement64].get.Add(i);
				}
			}
			if (instr.Op2Kind == OpKind.Memory && instr.MemoryDisplacement64 > 1)
			{
				if (stacks.ContainsKey(instr.MemoryDisplacement64))
					stacks[instr.MemoryDisplacement64].get.Add(i);
				else
				{
					stacks.Add(instr.MemoryDisplacement64, new StackSets());
					stacks[instr.MemoryDisplacement64].get.Add(i);
				}
			}
			if (instr.Op3Kind == OpKind.Memory && instr.MemoryDisplacement64 > 1)
			{
				if (stacks.ContainsKey(instr.MemoryDisplacement64))
					stacks[instr.MemoryDisplacement64].get.Add(i);
				else
				{
					stacks.Add(instr.MemoryDisplacement64, new StackSets());
					stacks[instr.MemoryDisplacement64].get.Add(i);
				}
			}
		}

		var ordered = stacks.OrderBy(a => a.Value.get.Count + a.Value.set.Count).ToArray();
		List<Register> FreeRegs = new List<Register>();
		if (!UsedRegs.Contains(Register.R15))
			FreeRegs.Add(Register.R15);
		if (!UsedRegs.Contains(Register.R14))
			FreeRegs.Add(Register.R14);
		if (!UsedRegs.Contains(Register.R13))
			FreeRegs.Add(Register.R13);
		if (!UsedRegs.Contains(Register.R12))
			FreeRegs.Add(Register.R12);
		if (!UsedRegs.Contains(Register.R11))
			FreeRegs.Add(Register.R11);
		if (!UsedRegs.Contains(Register.R10))
			FreeRegs.Add(Register.R10);
		if (!UsedRegs.Contains(Register.RSI))
			FreeRegs.Add(Register.RSI);
		//if (!UsedRegs.Contains(Register.RDI)) // throws error +_+
		//	FreeRegs.Add(Register.RDI);
		if (!UsedRegs.Contains(Register.R9) && stack.args < 3)
			FreeRegs.Add(Register.R9);
		if (!UsedRegs.Contains(Register.R8) && stack.args < 2)
			FreeRegs.Add(Register.R8);
		if (!UsedRegs.Contains(Register.RDX) && stack.args < 1)
			FreeRegs.Add(Register.RDX);
		//if (!UsedRegs.Contains(Register.RCX) && stack.args == 0) // throws error
		//	FreeRegs.Add(Register.RCX);

		for (i = 0; (i < FreeRegs.Count && i < ordered.Length); i++)
		{
			var target = ordered[i];
			var to = FreeRegs[i];
			foreach(var set in target.Value.set)
			{
				var manipulate = instructions[set];
				manipulate.Op0Kind = OpKind.Register;
				manipulate.Op0Register = to;
				instructions[set] = manipulate;
			}

			foreach (var get in target.Value.get)
			{
				var manipulate = instructions[get];
				if (manipulate.Op1Kind == OpKind.Memory)
				{
					manipulate.Op1Kind = OpKind.Register;
					manipulate.Op1Register = to;
				}
				else if (manipulate.Op2Kind == OpKind.Memory)
				{
					manipulate.Op2Kind = OpKind.Register;
					manipulate.Op2Register = to;
				}
				else if (manipulate.Op3Kind == OpKind.Memory)
				{
					manipulate.Op3Kind = OpKind.Register;
					manipulate.Op3Register = to;
				}
				instructions[get] = manipulate;
			}
		}

		ReduceStackMem(instructions, ordered.Skip(i).ToArray(), stack);
	}

	private static void RemoveDebugLocals(IList<Instruction> instructions)
	{
		for (int i = 0; i < instructions.Count - 1; i++)
		{
			var toMem = instructions[i];
			var fromMem = instructions[i+1];
			if (toMem.Code == Code.Mov_rm64_r64 && fromMem.Code == Code.Mov_r64_rm64 &&
				toMem.Op0Kind == OpKind.Memory && fromMem.Op1Kind == OpKind.Memory && 
				toMem.MemoryDisplacement64 == fromMem.MemoryDisplacement64)
			{
				var cnt = CountStackUse(toMem.MemoryBase, toMem.MemoryDisplacement64, instructions);
				if (cnt.sets == 1 && cnt.loads == 1)
				{
					if (toMem.Op1Register == fromMem.Op0Register)
					{
						instructions.RemoveAt(i);
						instructions.RemoveAt(i);
						continue;
					}
				}
			}
		}
	}

	private static void ReduceStackMem(IList<Instruction> instructions, KeyValuePair<ulong, StackSets>[] stackSets, StackInfo stack)
	{
		var RSPStart = stack.RSPBase;
		var newRSPMax = stackSets.Length * 16 + RSPStart;
		for(int i = 0; i < instructions.Count; i++)
		{
			var instr = instructions[i];
			if (instr.Code == Code.Add_rm64_imm32 && instr.Op0Register == Register.RSP)
			{
				instr.Immediate32 = (uint)newRSPMax;
				instructions[i] = instr;
			}
			else if (instr.Code == Code.Sub_rm64_imm32 && instr.Op0Register == Register.RSP)
			{
				instr.Immediate32 = (uint)newRSPMax;
				instructions[i] = instr;
			}
		}

		for(int idx = 0; idx < stackSets.Length; idx++)
		{
			var stackSet = stackSets[idx];
			foreach(var set in stackSet.Value.set)
			{
				var instr = instructions[set];
				instr.MemoryDisplacement64 = (ulong)(RSPStart + idx * 8);
				instructions[set] = instr;
			}
			foreach (var get in stackSet.Value.get)
			{
				var instr = instructions[get];
				instr.MemoryDisplacement64 = (ulong)(RSPStart + idx * 8);
				instructions[get] = instr;
			}
		}
	}

	private static void BuildRegShield(IList<Instruction> instructions, StackInfo stack)
	{
		List<Register> registers = new List<Register>();
		foreach(var instr in instructions)
		{
			if (instr.Op0Kind != OpKind.Register) continue;

			var fullreg = instr.Op0Register.GetFullRegister();
			if (fullreg == Register.RSP) continue;
			else if (fullreg == Register.RAX) continue;
			//else if (fullreg.ToString().StartsWith("R1")) continue; // idk why clr throw error when there PUSH R15 and POP R15
			else if (fullreg.ToString().StartsWith("ZMM")) continue;
			else if (fullreg.ToString().StartsWith("XMM")) continue;
			else if (fullreg == Register.None) continue;
			if (registers.Contains(fullreg)) continue;
			registers.Add(fullreg);
		}

		if (stack.args == 1)
		{
			registers.Remove(Register.RCX);
		}
		else if (stack.args == 2)
		{
			registers.Remove(Register.RCX);
			registers.Remove(Register.RDX);
		}
		else if (stack.args == 3)
		{
			registers.Remove(Register.RCX);
			registers.Remove(Register.RDX);
			registers.Remove(Register.R8);
		}
		else if (stack.args >= 4)
		{
			registers.Remove(Register.RCX);
			registers.Remove(Register.RDX);
			registers.Remove(Register.R8);
			registers.Remove(Register.R9);
		}

		foreach(var reg in registers) instructions.Insert(0, Instruction.Create(Code.Push_r64, reg));

		for(int i = 0; i < instructions.Count; i++)
		{
			if (instructions[i].Code == Code.Retnq)
			{
				foreach(var reg in registers)
				{
					instructions.Insert(i, Instruction.Create(Code.Pop_r64, reg));
					i++;
				}
			}
		}
		return;
	}

	struct StackSets
	{
		public List<int> set = new List<int>();
		public List<int> get = new List<int>();

		public StackSets()
		{
		}
	}

	private static (int sets, int loads) CountStackUse(Register baseReg, ulong displ, IList<Instruction> instructions)
	{
		if (baseReg != Register.RSP) return (0,0);
		int sets = 0;
		int loads = 0;
		foreach(var i in instructions)
		{
			if (i.MemoryBase != baseReg) continue;
			if (i.MemoryDisplacement64 != displ) continue;

			if (i.Op0Kind == OpKind.Memory)
				sets++;
			if (i.Op1Kind == OpKind.Memory)
				loads++;
			if (i.Op2Kind == OpKind.Memory)
				loads++;
			if (i.Op3Kind == OpKind.Memory)
				loads++;
			if (i.Op4Kind == OpKind.Memory)
				loads++;
		}
		return (sets, loads);
	}
}

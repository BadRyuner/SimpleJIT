using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Security.Cryptography.X509Certificates;
using Mono.Cecil;

namespace SimpleJIT
{
	internal class StackInfo
	{
		public Assembler asm;
		public Stack<AssemblerRegister64> UsedRegsHistory = new Stack<AssemblerRegister64>();
		public Dictionary<AssemblerRegister64, bool> IsRegFree = new Dictionary<AssemblerRegister64, bool>();

		internal int args;

		private bool WriteRSP = false;
		private int RSPSize = 0x28;
		internal int RSPBase = 0x28;

		public void FreeAllRegs()
		{
			for(int i = 0; i < IsRegFree.Count; i++)
				IsRegFree[IsRegFree.Keys.ElementAt(i)] = true;
		}

		public AssemblerRegister64 PopLastRegAndFree()
		{
			var reg = UsedRegsHistory.Pop();
			if (IsRegFree.ContainsKey(reg))
				IsRegFree[reg] = true;
			return reg;
		}

		public AssemblerRegister64 PopLastReg()
		{
			var reg = UsedRegsHistory.Pop();
			return reg;
		}

		public AssemblerRegister64 LastReg()
		{
			var reg = UsedRegsHistory.Peek();
			return reg;
		}

		public void LastRegToRAX() 
		{
			var reg = PopLastRegAndFree();
			asm.mov(rax, reg);
			IsRegFree[rax] = false;
			UsedRegsHistory.Push(rax);
		}

		public AssemblerRegister64 ReservRegister(bool isRegFree = false)
		{
			var freereg =IsRegFree.First(pair => pair.Value == true).Key;
			IsRegFree[freereg] = isRegFree;
			if (!isRegFree)
				UsedRegsHistory.Push(freereg);
			return freereg;
		}

		public AssemblerRegister64 GetArgument(byte idx, out bool error)
		{
			if (idx > 3)
			{
				error = true;
				return default;
			}

			error = false;

			switch (idx)
			{
				case 0:
					return rcx;
				case 1:
					return rdx;
				case 2:
					return r8;
				case 3:
					return r9;
				default:
					throw new Exception();
			}
		}

		public void Setup(int args, MethodDefinition md)
		{
			this.args = args;
			if (args == 0)
				IsRegFree.Add(rcx, true);
			if (args < 1)
				IsRegFree.Add(rdx, true);
			if (args < 2)
				IsRegFree.Add(r8, true);
			if (args < 3)
				IsRegFree.Add(r9, true);
			IsRegFree.Add(r10, true);
			IsRegFree.Add(r11, true);
			IsRegFree.Add(r12, true);
			IsRegFree.Add(r13, true);
			IsRegFree.Add(r14, true);
			IsRegFree.Add(r15, true);

			if (md.Body.Instructions.Any(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Call 
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Callvirt
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Calli
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Ldfld
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Ldflda
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Stloc
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Stloc_0
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Stloc_1
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Stloc_2
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Stloc_3
			|| i.OpCode.Code == Mono.Cecil.Cil.Code.Stloc_S))
			{
				WriteRSP = true;
				RSPSize += md.Body.Variables.Count * 8;
			}
		}

		public void WriteStart()
		{
			if (WriteRSP)
			{
				asm.sub(AssemblerRegisters.rsp, RSPSize);
			}
		}

		public void WriteEnd()
		{
			if (WriteRSP)
			{
				asm.add(AssemblerRegisters.rsp, RSPSize);
			}
		}
	}
}

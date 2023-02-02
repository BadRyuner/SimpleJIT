using Assembler = Iced.Intel.Assembler;
using Label = Iced.Intel.Label;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using SimpleJIT.Blocks.Jumps;
using System.Reflection;

namespace SimpleJIT.Blocks
{
	internal static class Utils
	{
		internal static List<BlockBase> ToBlocks(MethodDefinition md, Assembler asm)
		{
			var il = md.Body.Instructions;
			List<List<Instruction>> ilblocks = new List<List<Instruction>>();
			List<Instruction> currentblock = new List<Instruction>();

			int stack = 0;
			for(int i = 0; i < il.Count; i++)
			{
				var instr = il[i];
				//Console.WriteLine(instr.ToString());
				stack += StackChange(instr.OpCode.Code);

				if (instr.OpCode.Code == Code.Nop) continue;
				if (instr.OpCode.Code == Code.Br_S && il[i+1] == ((Instruction)instr.Operand)) continue;

				currentblock.Add(instr);
				if (stack == 0)
				{
					ilblocks.Add(currentblock);
					currentblock = new List<Instruction>();
					continue;
				}
			}

			if (currentblock.Count != 0)
				ilblocks.Add(currentblock);

			List<BlockBase> blocks = new List<BlockBase>();
			Dictionary<Instruction, BlockBase> AssociatedBlocks = new Dictionary<Instruction, BlockBase>();
			Dictionary<Instruction, Label> ToMarkWithLabel = new Dictionary<Instruction, Label>();

			for(int i = 0; i < ilblocks.Count; i++)
			{
				var ilblock = ilblocks[i];
				BlockBase last = null;
				for(int x = 0; x < ilblock.Count; x++)
				{
					var instr = ilblock[x];
					BlockBase block = null;
					switch(instr.OpCode.Code)
					{
						case Code.Ldnull:
						case Code.Ldc_I4_M1:
						case Code.Ldc_I4_0:
						case Code.Ldc_I4_1:
						case Code.Ldc_I4_2:
						case Code.Ldc_I4_3:
						case Code.Ldc_I4_4:
						case Code.Ldc_I4_5:
						case Code.Ldc_I4_6:
						case Code.Ldc_I4_7:
						case Code.Ldc_I4_S:
						case Code.Ldc_I4:
							block = new PushInt() { value = GetIntForLdc4(instr) };
							break;
						case Code.Ldc_I8:
							block = new PushInt() { value = (long)instr.Operand };
							break;

						case Code.Conv_U: // too very hard 4 me
						case Code.Conv_I:
						case Code.Conv_I1:
						case Code.Conv_I2:
						case Code.Conv_I4:
						case Code.Conv_I8:
						case Code.Conv_U1:
						case Code.Conv_U2:
						case Code.Conv_U4:
						case Code.Conv_U8:
							block = new NopBlock();
							break;

						case Code.Ldarg_0:
						case Code.Ldarg_1:
						case Code.Ldarg_2:
						case Code.Ldarg_3:
						case Code.Ldarg_S:
						case Code.Ldarg:
							block = new LoadArg() { at = GetArgId(instr) };
							break;

						case Code.Starg:
						case Code.Starg_S:
							block = new SaveArg() { arg = (byte)GetArgId(instr) };
							break;

						case Code.Add:
							block = new AddBlock();
							break;
						case Code.Sub:
							block = new SubBlock();
							break;
						case Code.Mul:
							block = new MulBlock();
							break;
						case Code.Div:
							block = new DivBlock();
							break;
						case Code.Rem:
							block = new RemBlock();
							break;

						case Code.Cgt:
							block = new CgtBlock();
							break;

						case Code.Ceq:
							block = new CgtBlock();
							break;

						case Code.Brtrue:
						case Code.Brtrue_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);

								block = new BrtrueBlock() { Target = label };
							}
							break;

						case Code.Brfalse:
						case Code.Brfalse_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);

								block = new BrfalseBlock() { Target = label };
							}
							break;

						case Code.Ble: 
						case Code.Ble_S:
						case Code.Ble_Un:
						case Code.Ble_Un_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);
								block = new BleBlock() { Target = label };
							}
							break;

						case Code.Bge:
						case Code.Bge_S:
						case Code.Bge_Un:
						case Code.Bge_Un_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);
								block = new BgeBlock() { Target = label };
							}
							break;

						case Code.Beq:
						case Code.Beq_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);
								block = new BeqBlock() { Target = label };
							}
							break;

						case Code.Bgt:
						case Code.Bgt_S:
						case Code.Bgt_Un:
						case Code.Bgt_Un_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);
								block = new BgtBlock() { Target = label };
							}
							break;

						case Code.Blt:
						case Code.Blt_S:
						case Code.Blt_Un:
						case Code.Blt_Un_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);
								block = new BltBlock() { Target = label };
							}
							break;

						case Code.Bne_Un:
						case Code.Bne_Un_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);
								block = new BneBlock() { Target = label };
							}
							break;

						case Code.Br:
						case Code.Br_S:
							{
								var jmp = (Instruction)instr.Operand;
								var label = asm.CreateLabel(jmp.Offset.ToString("X2"));
								if (AssociatedBlocks.TryGetValue(jmp, out BlockBase target))
									target.Label = label;
								else
									ToMarkWithLabel.Add(jmp, label);
								block = new BrBlock() { Target = label };
							}
							break;

						case Code.Stloc_0:
						case Code.Stloc_1:
						case Code.Stloc_2:
						case Code.Stloc_3:
						case Code.Stloc_S:
						case Code.Stloc:
							block = new StlocBlock() { 
								idx = instr.OpCode.Code == Code.Stloc_0 ? 0 :
								instr.OpCode.Code == Code.Stloc_1 ? 1 :
								instr.OpCode.Code == Code.Stloc_2 ? 2 :
								instr.OpCode.Code == Code.Stloc_3 ? 3 :
								((VariableDefinition)instr.Operand).Index
								};
							break;

						case Code.Ldloc_0:
						case Code.Ldloc_1:
						case Code.Ldloc_2:
						case Code.Ldloc_3:
						case Code.Ldloc_S:
						case Code.Ldloc:
							block = new LdlocBlock() {
								idx = instr.OpCode.Code == Code.Ldloc_0 ? 0 :
								instr.OpCode.Code == Code.Ldloc_1 ? 1 :
								instr.OpCode.Code == Code.Ldloc_2 ? 2 :
								instr.OpCode.Code == Code.Ldloc_3 ? 3 :
								((VariableDefinition)instr.Operand).Index
							};
							break;

						case Code.Ldsfld:
							{
								var field = (FieldReference)instr.Operand;
								var fieldtype = field.FieldType;
								block = new LdsfldBlock() { at = (ulong)SimpleJIT.Singletone.StaticFieldResolver.Invoke(field), 
									size = fieldtype.Name == "Byte" ? 1 : fieldtype.Name == "SByte" ? 1 : fieldtype.Name == "Boolean" ? 1 :
									fieldtype.Name == "Int16" ? 2 : fieldtype.Name == "UInt16" ? 2 :
									fieldtype.Name == "Int32" ? 4 : fieldtype.Name == "UInt32" ? 4 : 8};
							}
							break;

						case Code.Stsfld:
							{
								var field = (FieldReference)instr.Operand;
								var fieldtype = field.FieldType;
								block = new StsfldBlock()
								{
									at = (ulong)SimpleJIT.Singletone.StaticFieldResolver.Invoke(field),
									size = fieldtype.Name == "Byte" ? 1 : fieldtype.Name == "SByte" ? 1 : fieldtype.Name == "Boolean" ? 1 :
									fieldtype.Name == "Int16" ? 2 : fieldtype.Name == "UInt16" ? 2 :
									fieldtype.Name == "Int32" ? 4 : fieldtype.Name == "UInt32" ? 4 : 8
								};
							}
							break;

						case Code.Call:
							var CallTarget = (MethodReference)instr.Operand;
							block = new CallBlock() { target = SimpleJIT.Singletone.FunctionResolver.Invoke(CallTarget),
								ArgsCount = CallTarget.Parameters.Count,
								HasRet = CallTarget.ReturnType.Name != "Void" };
							break;

						case Code.Ret:
							block = new RetBlock() { shouldRetValue = md.ReturnType.Name != "Void" };
							break;
						default:
							throw new NotImplementedException($"{instr}");
					}
					block.Inner = last;
					AssociatedBlocks.Add(instr, block);
					//block.associatedWith = instr; // for debug
					if (ToMarkWithLabel.ContainsKey(instr))
					{
						block.Label = ToMarkWithLabel[instr];
						ToMarkWithLabel.Remove(instr);
					}
					last = block;
				}
				blocks.Add(last);
			}

			return blocks;
		}

		private static int GetIntForLdc4(Instruction instr)
		{
			switch(instr.OpCode.Code)
			{
				case Code.Ldc_I4_M1: return -1;
				case Code.Ldnull:
				case Code.Ldc_I4_0: return 0;
				case Code.Ldc_I4_1: return 1;
				case Code.Ldc_I4_2: return 2;
				case Code.Ldc_I4_3: return 3;
				case Code.Ldc_I4_4: return 4;
				case Code.Ldc_I4_5: return 5;
				case Code.Ldc_I4_6: return 6;
				case Code.Ldc_I4_7: return 7;
				case Code.Ldc_I4_S: return (sbyte)instr.Operand;
				case Code.Ldc_I4: return (int)instr.Operand;
				default:
					throw new NotImplementedException();
			}
		}

		private static int GetArgId(Instruction instr)
		{
			switch (instr.OpCode.Code)
			{
				case Code.Ldarg_0: return 0;
				case Code.Ldarg_1: return 1;
				case Code.Ldarg_2: return 2;
				case Code.Ldarg_3: return 3;
				case Code.Ldarg_S: return (byte)instr.Operand;
				case Code.Ldarg: return (int)instr.Operand;
				case Code.Starg_S:
				case Code.Starg: return ((ParameterDefinition)instr.Operand).Index;
				default:
					throw new NotImplementedException();
			}
		}


		private static int StackChange(Code code)
		{
			switch (code)
			{
				case Code.Br_S:
				case Code.Br:
				case Code.Nop:
				case Code.Ldind_I1:
				case Code.Ldind_U1:
				case Code.Ldind_I2:
				case Code.Ldind_U2:
				case Code.Ldind_I4:
				case Code.Ldind_U4:
				case Code.Ldind_I8:
				case Code.Ldind_I:
				case Code.Ldind_R4:
				case Code.Ldind_R8:
				case Code.Ldind_Ref:
				case Code.Neg:
				case Code.Conv_I1:
				case Code.Conv_I2:
				case Code.Conv_I4:
				case Code.Conv_I8:
				case Code.Conv_R4:
				case Code.Conv_R8:
				case Code.Conv_U4:
				case Code.Conv_U8:
				case Code.Conv_R_Un:
				case Code.Conv_Ovf_I1_Un:
				case Code.Conv_Ovf_I2_Un:
				case Code.Conv_Ovf_I4_Un:
				case Code.Conv_Ovf_I8_Un:
				case Code.Conv_Ovf_U1_Un:
				case Code.Conv_Ovf_U2_Un:
				case Code.Conv_Ovf_U4_Un:
				case Code.Conv_Ovf_U8_Un:
				case Code.Conv_Ovf_I_Un:
				case Code.Conv_Ovf_U_Un:
				case Code.Conv_Ovf_I1:
				case Code.Conv_Ovf_U1:
				case Code.Conv_Ovf_I2:
				case Code.Conv_Ovf_U2:
				case Code.Conv_Ovf_I4:
				case Code.Conv_Ovf_U4:
				case Code.Conv_Ovf_I8:
				case Code.Conv_Ovf_U8:
				case Code.Ldfld:
				case Code.Ldflda:
					return 0;

				case Code.Break:
				case Code.Switch:
				case Code.Jmp:
				case Code.Call:
				case Code.Calli:
					return 0;

				case Code.Ldc_I4_M1:
				case Code.Ldc_I4_0:
				case Code.Ldc_I4_1:
				case Code.Ldc_I4_2:
				case Code.Ldc_I4_3:
				case Code.Ldc_I4_4:
				case Code.Ldc_I4_5:
				case Code.Ldc_I4_6:
				case Code.Ldc_I4_7:
				case Code.Ldc_I4_8:
				case Code.Ldc_I4_S:
				case Code.Ldc_I4:
				case Code.Ldc_I8:
				case Code.Ldc_R4:
				case Code.Ldc_R8:
				case Code.Dup:
				case Code.Ldnull:
				case Code.Ldarg_0:
				case Code.Ldarg_1:
				case Code.Ldarg_2:
				case Code.Ldarg_3:
				case Code.Ldarg_S:
				case Code.Ldarga_S:
				case Code.Ldloc_0:
				case Code.Ldloc_1:
				case Code.Ldloc_2:
				case Code.Ldloc_3:
				case Code.Ldloc_S:
				case Code.Ldloca_S:
				case Code.Ldstr:
				case Code.Ldsfld:
				case Code.Ldsflda:
					return 1;

				case Code.Stloc_0:
				case Code.Stloc_1:
				case Code.Stloc_2:
				case Code.Stloc_3:
				case Code.Stloc_S:
				case Code.Starg_S:
				case Code.Pop:
				case Code.Ret:
				case Code.Brfalse_S:
				case Code.Brtrue_S:
				case Code.Beq_S:
				case Code.Bge_S:
				case Code.Bgt_S:
				case Code.Ble_S:
				case Code.Blt_S:
				case Code.Bne_Un_S:
				case Code.Bge_Un_S:
				case Code.Bgt_Un_S:
				case Code.Ble_Un_S:
				case Code.Blt_Un_S:
				case Code.Brfalse:
				case Code.Brtrue:
				case Code.Beq:
				case Code.Bge:
				case Code.Bgt:
				case Code.Ble:
				case Code.Blt:
				case Code.Bne_Un:
				case Code.Bge_Un:
				case Code.Bgt_Un:
				case Code.Ble_Un:
				case Code.Blt_Un:
				case Code.Ceq:
				case Code.Cgt:
				case Code.Cgt_Un:
				case Code.Clt:
				case Code.Clt_Un:
				case Code.Not:
				case Code.Add:
				case Code.Sub:
				case Code.Mul:
				case Code.Div:
				case Code.Div_Un:
				case Code.Rem:
				case Code.Rem_Un:
				case Code.And:
				case Code.Or:
				case Code.Xor:
				case Code.Shl:
				case Code.Shr:
				case Code.Shr_Un:
				case Code.Stsfld:
					return -1;

				case Code.Stind_Ref:
				case Code.Stind_I1:
				case Code.Stind_I2:
				case Code.Stind_I4:
				case Code.Stind_I8:
				case Code.Stind_R4:
				case Code.Stind_R8:
				case Code.Stfld:
					return -2;

				
				case Code.Callvirt:
				case Code.Cpobj:
				case Code.Ldobj:
				case Code.Newobj:
				case Code.Castclass:
				case Code.Isinst:
				case Code.Unbox:
				case Code.Throw:
				case Code.Stobj:
				case Code.Box:
				case Code.Newarr:
				case Code.Ldlen:
				case Code.Ldelema:
				case Code.Ldelem_I1:
				case Code.Ldelem_U1:
				case Code.Ldelem_I2:
				case Code.Ldelem_U2:
				case Code.Ldelem_I4:
				case Code.Ldelem_U4:
				case Code.Ldelem_I8:
				case Code.Ldelem_I:
				case Code.Ldelem_R4:
				case Code.Ldelem_R8:
				case Code.Ldelem_Ref:
				case Code.Stelem_I:
				case Code.Stelem_I1:
				case Code.Stelem_I2:
				case Code.Stelem_I4:
				case Code.Stelem_I8:
				case Code.Stelem_R4:
				case Code.Stelem_R8:
				case Code.Stelem_Ref:
				case Code.Ldelem_Any:
				case Code.Stelem_Any:
				case Code.Unbox_Any:
				case Code.Refanyval:
				case Code.Ckfinite:
				case Code.Mkrefany:
				case Code.Ldtoken:
				case Code.Conv_U2:
				case Code.Conv_U1:
				case Code.Conv_I:
				case Code.Conv_Ovf_I:
				case Code.Conv_Ovf_U:
				case Code.Add_Ovf:
				case Code.Add_Ovf_Un:
				case Code.Mul_Ovf:
				case Code.Mul_Ovf_Un:
				case Code.Sub_Ovf:
				case Code.Sub_Ovf_Un:
				case Code.Endfinally:
				case Code.Leave:
				case Code.Leave_S:
				case Code.Stind_I:
				case Code.Conv_U:
				case Code.Arglist:
				case Code.Ldftn:
				case Code.Ldvirtftn:
				case Code.Ldarg:
				case Code.Ldarga:
				case Code.Starg:
				case Code.Ldloc:
				case Code.Ldloca:
				case Code.Stloc:
				case Code.Localloc:
				case Code.Tail:
				case Code.Initobj:
				case Code.Constrained:
				case Code.Cpblk:
				case Code.Initblk:
				case Code.No:
				case Code.Rethrow:
				case Code.Sizeof:
				case Code.Refanytype:
				default:
					return 0; // idk why i want to calc stack and create blocks .-.
			}
			throw new NotImplementedException();
		}
	}
}

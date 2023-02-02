using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using System;
using System.Collections.Generic;

namespace SimpleJIT.Blocks;
internal class CallBlock : BlockBase
{
	internal IntPtr target;
	internal int ArgsCount;
	internal bool HasRet;

	static Dictionary<int, AssemblerRegister64> STDCALLArgs = new Dictionary<int, AssemblerRegister64>() { 
		[0] = AssemblerRegisters.rcx,
		[1] = AssemblerRegisters.rdx,
		[2] = AssemblerRegisters.r8,
		[3] = AssemblerRegisters.r9
		// 4 - inf -> stack
	};

	static Dictionary<int, AssemblerMemoryOperand> STDCALLArgsExt = new Dictionary<int, AssemblerMemoryOperand>()
	{
		[4] = AssemblerRegisters.rsp + 0x20,
		[5] = AssemblerRegisters.rsp + 0x28,
		[6] = AssemblerRegisters.rsp + 0x30,
		[7] = AssemblerRegisters.rsp + 0x38,
		[8] = AssemblerRegisters.rsp + 0x40,
		[9] = AssemblerRegisters.rsp + 0x48,
	};

	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		Dictionary<int, AssemblerRegister64> args = new Dictionary<int, AssemblerRegister64>();
		for(int i = ArgsCount; i > 0; i--)
		{
			args.Add(i - 1, stack.PopLastRegAndFree());
		}

		foreach(var arg in args)
		{
			if (STDCALLArgs.ContainsKey(arg.Key))
				asm.mov(STDCALLArgs[arg.Key], arg.Value);
			else
				asm.mov(__qword_ptr[STDCALLArgsExt[arg.Key]], arg.Value);
		}

		//Console.WriteLine(target.ToString("X2"));
		asm.call((ulong)target);

		if (HasRet)
			stack.UsedRegsHistory.Push(AssemblerRegisters.rax);
	}
}

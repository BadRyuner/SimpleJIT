using Iced.Intel;
using System.Collections.Generic;

namespace SimpleJIT.Blocks
{
	internal class RetBlock : BlockBase
	{
		internal bool shouldRetValue = false;

		internal override void Compile(Assembler asm, StackInfo stack)
		{
			base.Compile(asm, stack);
			if (shouldRetValue)
			{
				var retValue = stack.UsedRegsHistory.Pop();
				if (retValue.Value != Register.RAX)
					asm.mov(AssemblerRegisters.rax, retValue);
			}
			stack.WriteEnd();
			asm.ret();
		}
	}
}

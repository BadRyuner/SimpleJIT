using Iced.Intel;
using System;

namespace SimpleJIT.Blocks
{
	internal abstract class BlockBase
	{
		internal BlockBase Inner;
		internal Label Label;
		//internal Mono.Cecil.Cil.Instruction associatedWith;

		internal virtual void Compile(Assembler asm, StackInfo stack)
		{
			Inner?.Compile(asm, stack);
			if (!Label.IsEmpty)
			{
				asm.Label(ref Label);
			}
		}
	}
}

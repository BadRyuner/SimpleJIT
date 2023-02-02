using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleJIT.Blocks;
internal class LoadArg : BlockBase
{
	public int at;

	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		stack.UsedRegsHistory.Push(stack.GetArgument((byte)at, out bool error));
	}
}

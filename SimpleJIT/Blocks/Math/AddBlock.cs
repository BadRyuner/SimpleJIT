using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleJIT.Blocks;
internal class AddBlock : BlockBase
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		var from = stack.PopLastRegAndFree();
		var to = stack.LastReg();
		asm.add(to, from);
	}
}

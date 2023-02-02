using Iced.Intel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SimpleJIT.Blocks
{
	internal class PushInt : BlockBase
	{
		internal long value;

		internal override void Compile(Assembler asm, StackInfo stack)
		{
			base.Compile(asm, stack);
			asm.mov(stack.ReservRegister(), value);
		}
	}
}

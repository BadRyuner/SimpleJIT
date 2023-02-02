using Iced.Intel;
using System.Configuration;

namespace SimpleJIT.Blocks;
internal class CgtBlock : BlockBase
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		asm.cmp(stack.PopLastRegAndFree(), stack.PopLastRegAndFree());
		var result = stack.ReservRegister();
		asm.xor(result, result);
		asm.setg(result.Reg64ToReg8());
	}
}

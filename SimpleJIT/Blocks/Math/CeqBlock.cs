using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class CeqBlock : BlockBase
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		asm.cmp(stack.PopLastRegAndFree(), stack.PopLastRegAndFree());
		var result = stack.ReservRegister();
		asm.xor(result, result);
		asm.sete(result.Reg64ToReg8());
	}
}

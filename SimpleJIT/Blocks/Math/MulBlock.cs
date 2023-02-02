using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class MulBlock : BlockBase
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		var from = stack.PopLastRegAndFree();
		stack.LastRegToRAX();
		asm.imul(from);
	}
}

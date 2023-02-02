using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class BrfalseBlock : JumpBlock
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		var val = stack.PopLastRegAndFree();
		asm.cmp(val, 0);
		asm.je(Target);
	}
}

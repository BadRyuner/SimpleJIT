using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class BltBlock : JumpBlock
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		base.Compile(asm, stack);
		var val = stack.PopLastRegAndFree();
		var with = stack.PopLastRegAndFree();
		asm.cmp(with, val);
		asm.jl(Target);
	}
}

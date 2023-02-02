using Iced.Intel;

namespace SimpleJIT.Blocks.Jumps;
internal class BrBlock : JumpBlock
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		asm.jmp(Target);
	}
}

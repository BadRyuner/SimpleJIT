using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class RemBlock : DivBlock
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		asm.mov(AssemblerRegisters.rax, AssemblerRegisters.rdx);
	}
}

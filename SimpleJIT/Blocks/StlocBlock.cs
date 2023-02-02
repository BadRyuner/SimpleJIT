using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class StlocBlock : BlockBase
{
	internal int idx;

	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		asm.mov(AssemblerRegisters.rsp + (stack.RSPBase + idx*8), stack.PopLastRegAndFree());
	}
}

using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class LdlocBlock : BlockBase
{
	internal int idx;

	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		asm.mov(stack.ReservRegister(), AssemblerRegisters.rsp + (stack.RSPBase + idx*8));
	}
}

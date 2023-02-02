using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class SubBlock : BlockBase
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		var from = stack.PopLastRegAndFree();
		var to = stack.LastReg();
		asm.sub(to, from);
	}
}

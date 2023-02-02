using Iced.Intel;

namespace SimpleJIT.Blocks;
internal class DivBlock : BlockBase
{
	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		var from = stack.PopLastRegAndFree();
		if (from.Value == Register.RDX)
		{
			var newFrom = stack.ReservRegister(true);
			asm.mov(newFrom, from);
			from = newFrom;
		}
		stack.LastRegToRAX();
		asm.cqo();
		asm.idiv(from);
	}
}

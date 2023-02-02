using Iced.Intel;
namespace SimpleJIT.Blocks;
internal class SaveArg : BlockBase
{
	internal byte arg;

	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		asm.mov(stack.GetArgument(arg, out bool error), stack.PopLastRegAndFree());
	}
}

using Iced.Intel;
using System.Security.Cryptography;

namespace SimpleJIT.Blocks;
internal class LdsfldBlock : BlockBase
{
	internal ulong at;
	internal int size;

	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		var reg = stack.ReservRegister();
		switch (size)
		{
			case 8:
				asm.mov(reg, at);
				asm.mov(reg, AssemblerRegisters.__qword_ptr[reg]);
				break;
			case 4:
				asm.mov(reg, at);
				asm.mov(reg, AssemblerRegisters.__dword_ptr[reg]);
				break;
			case 2:
				asm.mov(reg, at);
				asm.mov(reg, AssemblerRegisters.__word_ptr[reg]);
				break;
			case 1:
				asm.mov(reg, at);
				asm.mov(reg, AssemblerRegisters.__byte_ptr[reg]);
				break;
			default:
				throw new System.NotImplementedException();
		}
	}
}

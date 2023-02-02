using Iced.Intel;
using System;
namespace SimpleJIT.Blocks;
internal class StsfldBlock : BlockBase
{
	internal ulong at;
	internal int size;

	internal override void Compile(Assembler asm, StackInfo stack)
	{
		base.Compile(asm, stack);
		var setAddr = stack.ReservRegister(true);
		var reg = stack.PopLastRegAndFree();
		switch (size)
		{
			case 8:
				asm.mov(setAddr, at);
				asm.mov(AssemblerRegisters.__qword_ptr[setAddr], reg);
				break;
			case 4:
				asm.mov(setAddr, at);
				asm.mov(AssemblerRegisters.__dword_ptr[setAddr], reg);
				break;
			case 2:
				asm.mov(setAddr, at);
				asm.mov(AssemblerRegisters.__word_ptr[setAddr], reg);
				break;
			case 1:
				asm.mov(setAddr, at);
				asm.mov(AssemblerRegisters.__byte_ptr[setAddr], reg);
				break;
			default:
				throw new System.NotImplementedException();
		}
	}
}

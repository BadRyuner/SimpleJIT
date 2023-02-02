using System;
using Iced.Intel;

namespace SimpleJIT.Blocks;
internal static class Extensions
{
	internal static AssemblerRegister8 Reg64ToReg8(this AssemblerRegister64 reg)
	{
		switch(reg.Value)
		{
			case Register.RAX:
				return AssemblerRegisters.al;
			case Register.RCX:
				return AssemblerRegisters.cl;
			case Register.RDX:
				return AssemblerRegisters.dl;
			case Register.RBX:
				return AssemblerRegisters.bl;
			case Register.RSP:
				return AssemblerRegisters.spl;
			case Register.RBP:
				return AssemblerRegisters.bpl;
			case Register.RSI:
				return AssemblerRegisters.sil;
			case Register.RDI:
				return AssemblerRegisters.dil;
			case Register.R8:
				return AssemblerRegisters.r8b;
			case Register.R9:
				return AssemblerRegisters.r9b;
			case Register.R10:
				return AssemblerRegisters.r10b;
			case Register.R11:
				return AssemblerRegisters.r11b;
			case Register.R12:
				return AssemblerRegisters.r12b;
			case Register.R13:
				return AssemblerRegisters.r13b;
			case Register.R14:
				return AssemblerRegisters.r14b;
			case Register.R15:
				return AssemblerRegisters.r15b;
			default:
				throw new NotImplementedException();
		}
	}

	internal static AssemblerRegister32 Reg64To32(this AssemblerRegister64 reg)
	{
		switch (reg.Value)
		{
			case Register.RAX:
				return AssemblerRegisters.eax;
			case Register.RCX:
				return AssemblerRegisters.ecx;
			case Register.RDX:
				return AssemblerRegisters.edx;
			case Register.RBX:
				return AssemblerRegisters.ebx;
			case Register.RSP:
				return AssemblerRegisters.esp;
			case Register.RBP:
				return AssemblerRegisters.ebp;
			case Register.RSI:
				return AssemblerRegisters.esi;
			case Register.RDI:
				return AssemblerRegisters.edi;
			case Register.R8:
				return AssemblerRegisters.r8d;
			case Register.R9:
				return AssemblerRegisters.r9d;
			case Register.R10:
				return AssemblerRegisters.r10d;
			case Register.R11:
				return AssemblerRegisters.r11d;
			case Register.R12:
				return AssemblerRegisters.r12d;
			case Register.R13:
				return AssemblerRegisters.r13d;
			case Register.R14:
				return AssemblerRegisters.r14d;
			case Register.R15:
				return AssemblerRegisters.r15d;
			default:
				throw new NotImplementedException();

		}
	}
}

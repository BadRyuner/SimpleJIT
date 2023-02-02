using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Iced.Intel;
using Mono.Cecil.Cil;
using System;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using Instruction = Mono.Cecil.Cil.Instruction;

namespace SimpleJITTests
{
	internal unsafe class Program
	{
		internal static void Main(string[] args)
		{
			//DoSomeThingsCecil();
			DoSomeThings();
			//DoBench();
			Console.ReadLine();
		}

		internal static void DoSomeThingsCecil()
		{
			SimpleJIT.SimpleJIT.PrintDebugInfo = true;
			var myjit = new SimpleJIT.SimpleJIT();

			var module = Mono.Cecil.ModuleDefinition.CreateModule("jitfrommem", Mono.Cecil.ModuleKind.Dll);
			var @long = module.ImportReference(typeof(long));
			var method = new Mono.Cecil.MethodDefinition("test", Mono.Cecil.MethodAttributes.Static, @long);
			method.Parameters.Add(new Mono.Cecil.ParameterDefinition("1", Mono.Cecil.ParameterAttributes.None, @long));
			method.Parameters.Add(new Mono.Cecil.ParameterDefinition("2", Mono.Cecil.ParameterAttributes.None, @long));
			method.Body = new Mono.Cecil.Cil.MethodBody(method);
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
			var ldarg0 = Instruction.Create(OpCodes.Ldarg_0);
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Bne_Un_S, ldarg0));
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, 500));
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
			method.Body.Instructions.Add(ldarg0);
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

			var jitted = (delegate* unmanaged[Stdcall]<long, long, long>)myjit.JitForCalli(method);
			long i = 2;
			for (int x = 1; x < 5; x++)
			{
				i = jitted(i, x);
				Console.WriteLine(i);
			}

			Console.WriteLine("\nIf run orig code:\n");
			i = 2;
			for (int x = 1; x < 5; x++)
			{
				//i = test(i, x);
				Console.WriteLine(i);
			}
		}

		internal static void DoSomeThings()
		{
			SimpleJIT.SimpleJIT.PrintDebugInfo = true;
			var myjit = new SimpleJIT.SimpleJIT();
			//var jitted = (delegate* unmanaged[Stdcall]<long, long, long>)myjit.JitForCalli<long, long, long>(test);
			var jitted = (delegate* <void>)myjit.JitForCalli(test);
			long i = 2;
			kek = 1;
			//jitted(0);
			for (int x = 1; x < 5; x++)
			{
				//i = jitted();
				jitted();
				Console.WriteLine(kek);
			}

			
			Console.WriteLine("\nIf run orig code:\n");
			i = 2;
			kek = 1;
			for (int x = 1; x < 5; x++)
			{
				//i = test();
				//i = test(i, x);
				test();
				Console.WriteLine(kek);
			}

		}

		internal static void DoBench() => BenchmarkRunner.Run<BenchIt>();

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static long Get() => 1;

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void test()
		{
			kek++;
		}

		static byte kek = 1;

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void testcall(int i)
		{
			for (i = 1; i <= 100; i++)
			{
				if (i % 3 == 0 && i % 5 == 0)
				{
					WriteFizzBuzz();
				}
				else if (i % 3 == 0)
				{
					WriteFizz();
				}
				else if (i % 5 == 0)
				{
					WriteBuzz();
				}
				else
				{
					Console.WriteLine(i);
				}
			}
		}

		static void WriteFizzBuzz() => Console.WriteLine("FizzBuzz");
		static void WriteFizz() => Console.WriteLine("Fizz");
		static void WriteBuzz() => Console.WriteLine("Buzz");

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static long ret1() => 1;
	}

	public unsafe class BenchIt
	{
		public IntPtr MyJITTest;
		public IntPtr TestPtr;

		[GlobalSetup] public void Setup()
		{
			var jit = new SimpleJIT.SimpleJIT();
			MyJITTest = jit.JitForCalli<long>(test);

			TestPtr = (IntPtr)(delegate* <long>)&test;
		}

		[Benchmark] public long MyJIT()
		{
			return ((delegate* managed <long>)MyJITTest)();
		}

		[Benchmark(Baseline = true)] public long SharpJIT()
		{
			//return test();
			return ((delegate* managed <long>)TestPtr)();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static long test()
		{
			long a = Get(); long b = Get();
			long c = Get(); long d = Get();
			long g = Get(); long x = Get();
			long y = Get(); long n = Get();
			long nn = Get(); long nnn = Get();
			long nnnn = Get(); long nnnnn = Get();
			long cc = Get();

			return a + b + c + d + g + x + y + n + nn + nnn + nnnn + nnnnn + cc;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static long Get() => 1;
	}
}

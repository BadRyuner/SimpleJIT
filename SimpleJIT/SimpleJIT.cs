using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Iced.Intel;
using Mono.Cecil;
using Mono.Collections.Generic;
using SimpleJIT.Blocks;

namespace SimpleJIT
{
	public unsafe class SimpleJIT : IDisposable
	{
		public delegate IntPtr ResolveFunction(MethodReference target);
		public delegate IntPtr ResolveField(FieldReference target);

		public static SimpleJIT Singletone;
		public static Dictionary<MethodReference, IntPtr> ResolvedFunctionsCache = new Dictionary<MethodReference, IntPtr>();
		public static Dictionary<FieldReference, IntPtr> ResolvedStaticFieldsCache = new Dictionary<FieldReference, IntPtr>();
		public static bool PrintDebugInfo = false;

		//public List<Detour> JittedMethods = new List<Detour>();
		public ResolveFunction FunctionResolver;
		public ResolveField StaticFieldResolver;

		private List<GCHandle> AllocedMethods = new List<GCHandle>();

		public SimpleJIT()
		{
			if (Singletone == null) Singletone = this;
			FunctionResolver += ResolveFunctionFromClr;
			StaticFieldResolver += ResolveStaticFieldFromClr;
		}

		private static BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty;
		private static IntPtr ResolveFunctionFromClr(MethodReference target)
		{
			if (ResolvedFunctionsCache.ContainsKey(target)) return ResolvedFunctionsCache[target];

			if (target.DeclaringType == null) return IntPtr.Zero;
			var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(asm => asm.DefinedTypes).FirstOrDefault(t => t.FullName == target.DeclaringType.FullName);
			if (type == null) return IntPtr.Zero;
			var method = type.GetMethods(All).FirstOrDefault(m => m.Name == target.Name && CheckParams(target.Parameters, m.GetParameters()));
			if (method == null) return IntPtr.Zero;
			var returnValue = method.MethodHandle.GetFunctionPointer();
			ResolvedFunctionsCache.Add(target, returnValue);
			return returnValue;
		}

		private static IntPtr ResolveStaticFieldFromClr(FieldReference target)
		{
			if (ResolvedStaticFieldsCache.ContainsKey(target)) return ResolvedStaticFieldsCache[target];

			if (target.DeclaringType == null) return IntPtr.Zero;
			var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(asm => asm.DefinedTypes).FirstOrDefault(t => t.FullName == target.DeclaringType.FullName);
			if (type == null) return IntPtr.Zero;
			var field = type.GetFields(All).FirstOrDefault(f => f.Name == target.Name);
			if (field == null) return IntPtr.Zero;
			var returnValue = UnsafeUtils.GetStaticFieldPointer(field);
			ResolvedStaticFieldsCache.Add(target, returnValue);
			return returnValue;
		}

		//public void JitAndReplace<T>(Func<T> target) =>
		//	AttachJittedMethod(JitInternal(GetMDForMI(target.Method)), target.Method);

		//public void JitAndReplace<T1, T>(Func<T1, T> target) =>
		//	AttachJittedMethod(JitInternal(GetMDForMI(target.Method)), target.Method);

		//public void JitAndReplace<T1, T2, T>(Func<T1, T2, T> target) =>
		//	AttachJittedMethod(JitInternal(GetMDForMI(target.Method)), target.Method);

		public IntPtr JitForCalli<T>(Func<T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T>(Func<T1, T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T2, T>(Func<T1, T2, T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T2, T3, T>(Func<T1, T2, T3, T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T2, T3, T4, T>(Func<T1, T2, T3, T4, T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T2, T3, T4, T5, T>(Func<T1, T2, T3, T4, T5, T> target) => JitInternal(GetMDForMI(target.Method));

		public IntPtr JitForCalli(Action target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T>(Action<T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T>(Action<T1, T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T2, T>(Action<T1, T2, T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T2, T3, T>(Action<T1, T2, T3, T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T2, T3, T4, T>(Action<T1, T2, T3, T4, T> target) => JitInternal(GetMDForMI(target.Method));
		public IntPtr JitForCalli<T1, T2, T3, T4, T5, T>(Action<T1, T2, T3, T4, T5, T> target) => JitInternal(GetMDForMI(target.Method));

		public IntPtr JitForCalli(MethodDefinition notRealMethod) => JitInternal(notRealMethod);

		/* private void AttachJittedMethod(IntPtr jitted, MethodInfo from)
		{
			RuntimeHelpers.PrepareMethod(from.MethodHandle);
			var isExists = JittedMethods.FirstOrDefault(jm => jm.Method == from);
			if (isExists != null)
				isExists.Free();
			JittedMethods.Add(new Detour(from, (IntPtr)jitted));
		} */

		private IntPtr JitInternal(MethodDefinition target)
		{
			if (PrintDebugInfo)
			{
				Console.WriteLine($"Jitting method -> {target.FullName}\n");
			}

			var asm = new Assembler(64);
			var blocks = Utils.ToBlocks(target, asm);
			StackInfo stack = new StackInfo() { asm = asm };
			stack.Setup(target.Parameters.Count, target);
			stack.WriteStart();
			foreach(var block in blocks)
				block.Compile(asm, stack);

			if (PrintDebugInfo)
			{	
				Console.WriteLine("Before Optimizations & Safety Additions: ");
				foreach (var i in asm.Instructions)
					Console.WriteLine(i.ToString());
				Console.WriteLine();
			}

			var output = Optimizer.Optimize(asm, stack);

			if (PrintDebugInfo)
			{
				Console.WriteLine("After Optimizations: ");
				foreach (var i in asm.Instructions)
					Console.WriteLine(i.ToString());
				Console.WriteLine();
			}

			var mem = GCHandle.Alloc(output, GCHandleType.Pinned);
			AllocedMethods.Add(mem);

			VirtualProtect(mem.AddrOfPinnedObject(), (UIntPtr)output.Length, 0x40, out var _);

			if (PrintDebugInfo)
			{
				Console.WriteLine($"Jitted at -> {((long)mem.AddrOfPinnedObject()).ToString("X2")}\n");
			}

			return mem.AddrOfPinnedObject();
		}

		private static MethodDefinition GetMDForMI(MethodInfo mi)
		{
			var inAsm = mi.DeclaringType.Assembly;
			if (AsmToDef.TryGetValue(inAsm, out var asmdef));
			else
			{
				asmdef = AssemblyDefinition.ReadAssembly(inAsm.Location);
				AsmToDef.Add(inAsm, asmdef);
			}
			return asmdef.MainModule.GetType(mi.DeclaringType.FullName).Methods.First(method => method.Name == mi.Name && CheckParams(method.Parameters, mi.GetParameters()));
		}

		private static bool CheckParams(Collection<ParameterDefinition> fromdef, ParameterInfo[] fromsharp)
		{
			if (fromdef.Count != fromsharp.Length) return false;
			
			for(int i = 0; i < fromdef.Count; i++)
			{
				var p1 = fromdef[i];
				var p2 = fromsharp[i];
				if (p1.ParameterType.FullName != p2.ParameterType.FullName) return false;
			}
			return true;
		}

		private static Dictionary<Assembly, AssemblyDefinition> AsmToDef = new Dictionary<Assembly, AssemblyDefinition>();

		public void Dispose()
		{
			//foreach(var jm in JittedMethods)
			//	jm.Free();

			foreach(var handle in AllocedMethods)
				handle.Free();

			if (Singletone == this) Singletone = null;
		}

		[DllImport("kernel32.dll")]
		static extern bool VirtualProtect(IntPtr lpAddress,
			UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
	}
}

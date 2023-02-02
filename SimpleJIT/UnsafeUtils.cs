using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace SimpleJIT;
internal static class UnsafeUtils
{
	internal static IntPtr GetStaticFieldPointer(FieldInfo StaticField)
	{
		DynamicMethod dm = new DynamicMethod("GetStaticField", MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(IntPtr), new Type[] { }, typeof(UnsafeUtils), true);
		var ilgen = dm.GetILGenerator();
		ilgen.Emit(OpCodes.Ldsflda, StaticField);
		ilgen.Emit(OpCodes.Conv_I);
		ilgen.Emit(OpCodes.Ret);
		return (IntPtr)dm.Invoke(null, null);
	}
}

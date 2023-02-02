# SimpleJIT
Not a very smart JIT compiler for C# instructions.
Not all OpCodes are supported.
# Example
How to jit:
```csharp
var jit = new SimpleJIT.SimpleJIT();
var MyMethod = (delegate* managed <void>)jit.JitForCalli(MyCoolMethod);
MyMethod(); // invoke

static void MyCoolMethod() {
  // some things
}
```
How to jit from Cecil:
```csharp
// define your module and method
var module = Mono.Cecil.ModuleDefinition.CreateModule("jitfrommem", Mono.Cecil.ModuleKind.Dll);
var @long = module.ImportReference(typeof(long));
var method = new Mono.Cecil.MethodDefinition("MAX", Mono.Cecil.MethodAttributes.Static, @long);
method.Parameters.Add(new Mono.Cecil.ParameterDefinition("1", Mono.Cecil.ParameterAttributes.None, @long));
method.Parameters.Add(new Mono.Cecil.ParameterDefinition("2", Mono.Cecil.ParameterAttributes.None, @long));
method.Body = new MethodBody(method);
method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
var ldarg0 = Instruction.Create(OpCodes.Ldarg_0);
method.Body.Instructions.Add(Instruction.Create(OpCodes.Bgt_S, ldarg0));
method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
method.Body.Instructions.Add(ldarg0);
method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
// create jit instance
var jit = new SimpleJIT.SimpleJIT();
// jit cecil method
var jitted = (delegate* managed <long, long, long>)myjit.JitForCalli(method);
// call
var result = jitted(400, 200); // result = 400
```

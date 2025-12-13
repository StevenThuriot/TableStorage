using System.Reflection;
using System.Reflection.Emit;

namespace TableStorage.Fluent;

internal static class DollarTypeClass
{
    static DollarTypeClass()
    {
        AssemblyName assemblyName = new("DollarTypes");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("FluentModule");
        TypeBuilder typeBuilder = moduleBuilder.DefineType("DollarType", TypeAttributes.Public);

        FieldBuilder fieldBuilder = typeBuilder.DefineField("_type", typeof(string), FieldAttributes.Private);
        PropertyBuilder propertyBuilder = typeBuilder.DefineProperty("$type", PropertyAttributes.HasDefault, typeof(string), null);

        const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

        MethodBuilder getMethodBuilder = typeBuilder.DefineMethod("get_$type", methodAttributes, typeof(string), Type.EmptyTypes);
        ILGenerator getIL = getMethodBuilder.GetILGenerator();
        getIL.Emit(OpCodes.Ldarg_0);
        getIL.Emit(OpCodes.Ldfld, fieldBuilder);
        getIL.Emit(OpCodes.Ret);
        propertyBuilder.SetGetMethod(getMethodBuilder);

        MethodBuilder setMethodBuilder = typeBuilder.DefineMethod("set_$type", methodAttributes, null, [typeof(string)]);
        ILGenerator setIL = setMethodBuilder.GetILGenerator();
        setIL.Emit(OpCodes.Ldarg_0);
        setIL.Emit(OpCodes.Ldarg_1);
        setIL.Emit(OpCodes.Stfld, fieldBuilder);
        setIL.Emit(OpCodes.Ret);
        propertyBuilder.SetSetMethod(setMethodBuilder);

        Type = typeBuilder.CreateType();
    }

    public static Type Type { get; }
}

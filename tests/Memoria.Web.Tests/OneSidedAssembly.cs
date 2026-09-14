using System;
using System.Reflection;
using System.Reflection.Emit;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;

namespace Memoria.Web.Tests;

/// <summary>
/// An assembly declaring a type under one consistency model and nothing under the other.
/// </summary>
/// <remarks>
/// Emitted rather than compiled, because this test assembly carries both models' samples and the
/// registry scans an assembly whole: there is no way to hand it half of this one. Each holds a
/// single identifier — a stream on one side, a DCB projection id on the other — which is the least
/// a domain can declare and still be registered under a model. The getters answer nothing useful;
/// no page these are used with reads them.
/// </remarks>
internal static class OneSidedAssembly
{
    /// <summary>One stream id, and nothing of the DCB model.</summary>
    public static readonly Assembly Streamed = Declaring("StreamedOnly", "OnlyStreamId", typeof(IStreamId));

    /// <summary>One DCB projection id, and nothing of the streamed model.</summary>
    public static readonly Assembly Dcb = Declaring("DcbOnly", "OnlyProjectionId", typeof(IDcbProjectionId));

    private static Assembly Declaring(string assemblyName, string typeName, Type contract)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName);
        var type = module.DefineType(
            $"{assemblyName}.{typeName}", TypeAttributes.Public | TypeAttributes.Class, typeof(object), [contract]);

        foreach (var property in contract.GetProperties())
        {
            var getter = type.DefineMethod(
                $"get_{property.Name}",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                property.PropertyType,
                Type.EmptyTypes);

            var il = getter.GetILGenerator();
            if (property.PropertyType == typeof(string))
            {
                il.Emit(OpCodes.Ldstr, "only");
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            il.Emit(OpCodes.Ret);

            type.DefineMethodOverride(getter, property.GetGetMethod()!);
            type.DefineProperty(property.Name, PropertyAttributes.None, property.PropertyType, null).SetGetMethod(getter);
        }

        type.CreateType();
        return assembly;
    }
}

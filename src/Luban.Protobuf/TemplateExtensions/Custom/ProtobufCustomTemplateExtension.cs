using Luban.Defs;
using Luban.Utils;
using Scriban.Runtime;

namespace Luban.Protobuf.TemplateExtensions;

/// <summary> 该模板文件扩展所有方法都会使用 xxx_xxx var来调用，驼峰写法的转置 </summary>
public class ProtobufCustomTemplateExtension : ScriptObject
{
  /// <summary> 该函数将会在模板文件.sbn里面使用full_name 变量var调用</summary>
  public static string FullName(DefTypeBase type)
  {
    return TypeUtil.MakePbFullName(type.Namespace, type.Name);
  }
  
  
}

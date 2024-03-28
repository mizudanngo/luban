using Luban.CodeTarget;
using Luban.CSharp.CodeTarget;
using Luban.CSharp.TemplateExtensions;
using Luban.Defs;
using Luban.Protobuf.TemplateExtensions;
using Luban.Utils;
using Scriban;
using Scriban.Runtime;

namespace Luban.Protobuf.CodeTarget;


[CodeTarget("protobuf3-custom-cs-json")]
public class Protobuf3CsharpSimpleJsonCodeTarget : CsharpCodeTargetBase
{
  public override string FileHeader => CommonFileHeaders.AUTO_GENERATE_C_LIKE;
  
  protected override string FileSuffixName => "g.cs";
  
  protected override string TemplateDir => "pb";

  protected override string GetFileNameWithoutExtByTypeName(string name) => TypeUtil.MakeGoNamespace(name);
  
  public override void Handle(GenerationContext ctx, OutputFileManifest manifest)
  {
    var tasks = new List<Task<OutputFile>>();

    tasks.Add(Task.Run(() =>
    {
      var writer = new CodeWriter();
      GenerateTables(ctx, ctx.ExportTables, writer);
      return new OutputFile() { File = $"{GetFileNameWithoutExtByTypeName(ctx.Target.Manager)}.{FileSuffixName}", Content = writer.ToResult(FileHeader) };
    }));
    
    foreach (var table in ctx.ExportTables)
    {
      tasks.Add(Task.Run(() =>
      {
        var writer = new CodeWriter();
        GenerateTable(ctx, table, writer);
        return new OutputFile() { File = $"{GetFileNameWithoutExtByTypeName(table.FullName)}.{FileSuffixName}", Content = writer.ToResult(FileHeader) };
      }));
    }

    Task.WaitAll(tasks.ToArray());
    foreach (var task in tasks)
    {
      manifest.AddFile(task.Result);
    }
  }

  public override void GenerateTables(GenerationContext ctx, List<DefTable> tables, CodeWriter writer)
  {
    var template = GetTemplate("tables-json");
    var tplCtx = CreateTemplateContext(template);
    // 添加自定义模板
    tplCtx.PushGlobal(new ProtobufCustomTemplateExtension());
    var extraEnvs = new ScriptObject
    {
      { "__ctx", ctx},
      { "__name", ctx.Target.Manager },
      { "__namespace", ctx.Target.TopModule },
      { "__tables", tables },
      { "__code_style", CodeStyle},
    };
    tplCtx.PushGlobal(extraEnvs);
    writer.Write(template.Render(tplCtx));
  }

  public override void GenerateTable(GenerationContext ctx, DefTable table, CodeWriter writer)
  {
    var template = GetTemplate("table-json");
    var tplCtx = CreateTemplateContext(template);
    // 添加自定义模板
    tplCtx.PushGlobal(new ProtobufCustomTemplateExtension());
    var extraEnvs = new ScriptObject
    {
      { "__ctx", ctx},
      { "__name", TypeUtil.MakeGoNamespace(table.FullName) },
      { "__namespace", ctx.Target.TopModule },
      { "__table", table },
      { "__this", table },
      { "__key_type", table.KeyTType},
      { "__value_type", table.ValueTType},
      { "__code_style", CodeStyle},
    };
    tplCtx.PushGlobal(extraEnvs);
    writer.Write(template.Render(tplCtx));
  }
  
  protected override void OnCreateTemplateContext(TemplateContext ctx)
  {
    base.OnCreateTemplateContext(ctx);
    ctx.PushGlobal(new CsharpSimpleJsonTemplateExtension());
  }   
}

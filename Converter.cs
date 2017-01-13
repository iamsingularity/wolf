using System;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Wolf
{
  internal class Converter
  {
    private Config _config;
    private Index _index;

    internal Converter(Config config)
    {
      _config = config;
      _index = new Index(config);
    }

    internal void Run()
    {
      try
      {
        var dirs = Directory.GetDirectories(_config.InputDirectory).Select(d => new DirectoryInfo(d));
        foreach (DirectoryInfo d in dirs)
        {
          var mdFile = GetMarkdownFileName(d);
          if (mdFile == null)
          {
            Log.Error($"No Markdown (.md) document found in directory '{d.FullName}'");
          }
          else
          {
            Convert(mdFile);
          }
        }
        _index.Save();
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    private static FileInfo GetMarkdownFileName(DirectoryInfo dir)
    {
      var names = new[] { dir.Name, "index", "default", "post" };
      foreach (var n in names)
      {
        var fi = new FileInfo(Path.ChangeExtension(Path.Combine(dir.FullName, n), "md"));
        if (fi.Exists)
        {
          return fi;
        }
      }
      return null;
    }

    private void Convert(FileInfo mdFile)
    {
      var name =  mdFile.Directory.Name;
      var htmlDir = Path.Combine(_config.OutputDirectory, name);
      if (!Directory.Exists(htmlDir))
      {
        Directory.CreateDirectory(htmlDir);
      }
      using (var reader = mdFile.OpenText())
      {
        var pipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();
        var doc = Markdown.Parse(reader.ReadToEnd(), pipeline);
        var yaml = doc.Descendants().OfType<YamlFrontMatterBlock>().FirstOrDefault();
        if (yaml != null)
        {
          _index.Add(name, yaml.Lines.Lines.Select(l => l.ToString()));
        }
        // Actual HTML conversion now?
        var htmlFile = Path.ChangeExtension(Path.Combine(htmlDir, name), "html");
        if (_config.ForceGeneration || !File.Exists(htmlFile))
        {
          Log.Info($"Processing '{htmlFile}'");
          foreach (var l in doc.Descendants().OfType<LinkInline>())
          {
            var img = new FileInfo(Path.Combine(mdFile.DirectoryName, l.Url));
            if (img.Exists)
            {
              img.CopyTo(Path.Combine(htmlDir, l.Url), true);
              l.Url = _config.ImagePrefix + name + '/' + l.Url;
            }
          }
          using (var writer = new StreamWriter(new FileStream(htmlFile, FileMode.Create)))
          {
            var renderer = new HtmlRenderer(writer);
            pipeline.Setup(renderer);
            renderer.Render(doc);
            writer.Flush();
          }
        }
      }
    }
  }
}
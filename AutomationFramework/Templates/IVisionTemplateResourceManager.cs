namespace AutomationFramework.Templates;

public interface IVisionTemplateResourceManager
{
    VisionTemplateLease Acquire(string templateFilePath);
    Dictionary<string, VisionTemplateLease> Acquire(params string[] templateFilePaths);
}
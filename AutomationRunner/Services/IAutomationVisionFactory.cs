using AutomationFramework;
using OpenCvSharp;

namespace AutomationRunner.Services;

public interface IAutomationVisionFactory
{
    Vision Create(TemplateMatchModes templateMatchMode = TemplateMatchModes.CCoeffNormed);
}

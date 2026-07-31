using System.Drawing;

namespace AutomationFramework.Models;

public sealed record MatchCandidate(double Scale, Point Location, double Confidence);

using AppFitness.Shared.Services;

namespace AppFitness.Wasm.Services;

public class FormFactor : IFormFactor
{
    public string GetFormFactor() => "Web";

    public string GetPlatform() => "WebAssembly";
}

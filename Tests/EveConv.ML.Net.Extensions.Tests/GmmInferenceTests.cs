[assembly: CaptureConsole]
namespace EveConv.ML.Net.Extensions.Tests;

public class GmmInferenceTests
{
    [Fact]
    public void GmmInference_Demo()
    {
        // 1) demo data: 3 blobs in 2D
        var data = BuildSyntheticData();

        // 2) fit by BIC
        var (best, all) = GmmInference.FitByBic(
            x: data,
            kMin: 1,
            kMax: 6,
            useDiagonalCovariance: false, // true for faster/robuster in high-D
            seed: 42,
            maxIterations: 150);

        // 3) print BIC table
        Console.WriteLine("K\tLogEvidenceApprox\tBIC");
        foreach (var r in all.OrderBy(t => t.K))
            Console.WriteLine($"{r.K}\t{r.LogEvidenceApprox:F4}\t\t{r.BIC:F4}");

        Console.WriteLine($"\nBest K = {best.K}, BIC = {best.BIC:F4}");

        // 4) predict
        int[] labels = GmmInference.Predict(best, data);
        Console.WriteLine("First 20 labels: " + string.Join(", ", labels.Take(20)));

        // 5) soft probabilities
        var proba = GmmInference.PredictProba(best, data.Take(5).ToArray());
        Console.WriteLine("\nFirst 5 samples posterior probs:");
        for (int i = 0; i < proba.Length; i++)
        {
            Console.WriteLine($"{i}: [{string.Join(", ", proba[i].Select(p => p.ToString("F4")))}]");
        }
    }

    private static double[][] BuildSyntheticData()
    {
        var rnd = new Random(7);
        var list = new List<double[]>();

        // cluster A around (0,0)
        for (int i = 0; i < 120; i++)
            list.Add(new[] { NextGaussian(rnd, 0.0, 0.6), NextGaussian(rnd, 0.0, 0.5) });

        // cluster B around (5,5)
        for (int i = 0; i < 110; i++)
            list.Add(new[] { NextGaussian(rnd, 5.0, 0.7), NextGaussian(rnd, 5.0, 0.7) });

        // cluster C around (9,1)
        for (int i = 0; i < 100; i++)
            list.Add(new[] { NextGaussian(rnd, 9.0, 0.5), NextGaussian(rnd, 1.0, 0.4) });

        return list.ToArray();
    }

    // Box-Muller
    private static double NextGaussian(Random rnd, double mean, double std)
    {
        double u1 = 1.0 - rnd.NextDouble();
        double u2 = 1.0 - rnd.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + std * z;
    }
}

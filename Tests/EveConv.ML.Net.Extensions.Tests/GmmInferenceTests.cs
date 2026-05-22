[assembly: CaptureConsole]
namespace EveConv.ML.Net.Extensions.Tests;

public class GmmInferenceTests
{
    [Fact]
    public void FitByBic_WellSeparatedBlobs_SelectsThreeComponents()
    {
        var data = BuildSyntheticData();

        var (best, all) = GmmInference.FitByBic(
            x: data,
            kMin: 1,
            kMax: 6,
            useDiagonalCovariance: false,
            seed: 42,
            maxIterations: 150);

        Console.WriteLine("K\tLogLikelihood\tBIC");
        foreach (var r in all.OrderBy(t => t.K))
            Console.WriteLine($"{r.K}\t{r.LogLikelihood:F4}\t\t{r.BIC:F4}");

        Console.WriteLine($"\nBest K = {best.K}, BIC = {best.BIC:F4}");

        var labels = GmmInference.Predict(best, data);
        Console.WriteLine("First 20 labels: " + string.Join(", ", labels.Take(20)));

        var proba = GmmInference.PredictProba(best, new[]
        {
            data[0],
            data[130],
            data[260]
        });

        Console.WriteLine("\nRepresentative posterior probs:");
        for (var i = 0; i < proba.Length; i++)
        {
            Console.WriteLine($"{i}: [{string.Join(", ", proba[i].Select(p => p.ToString("F4")))}]");
        }

        Assert.Equal(3, best.K);

        var bicByK = all.ToDictionary(x => x.K, x => x.BIC);
        Assert.True(bicByK[3] < bicByK[2]);
        Assert.True(bicByK[3] < bicByK[4]);

        var assignments = proba.Select(ArgMax).ToArray();
        Assert.Equal(3, assignments.Distinct().Count());
        Assert.All(proba, row => Assert.InRange(row.Max(), 0.80, 1.0));
    }

    private static double[][] BuildSyntheticData()
    {
        var rnd = new Random(7);
        var list = new List<double[]>();

        // cluster A around (0,0)
        for (var i = 0; i < 120; i++)
            list.Add(new[] { NextGaussian(rnd, 0.0, 0.6), NextGaussian(rnd, 0.0, 0.5) });

        // cluster B around (5,5)
        for (var i = 0; i < 110; i++)
            list.Add(new[] { NextGaussian(rnd, 5.0, 0.7), NextGaussian(rnd, 5.0, 0.7) });

        // cluster C around (9,1)
        for (var i = 0; i < 100; i++)
            list.Add(new[] { NextGaussian(rnd, 9.0, 0.5), NextGaussian(rnd, 1.0, 0.4) });

        return list.ToArray();
    }

    // Box-Muller
    private static double NextGaussian(Random rnd, double mean, double std)
    {
        var u1 = 1.0 - rnd.NextDouble();
        var u2 = 1.0 - rnd.NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + std * z;
    }

    private static int ArgMax(IReadOnlyList<double> values)
    {
        var best = 0;
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] > values[best])
            {
                best = i;
            }
        }

        return best;
    }
}

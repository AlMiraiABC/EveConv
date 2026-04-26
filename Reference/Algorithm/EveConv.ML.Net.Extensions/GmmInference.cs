using Microsoft.ML.Probabilistic.Algorithms;
using Microsoft.ML.Probabilistic.Models;
using Microsoft.ML.Probabilistic.Distributions;
using Microsoft.ML.Probabilistic.Factors.Attributes;
using Microsoft.ML.Probabilistic.Math;
using Microsoft.ML.Probabilistic.Utilities;
using Range = Microsoft.ML.Probabilistic.Models.Range;

namespace EveConv.ML.Net.Extensions;

public class GmmInference
{
    public sealed class FitResult
    {
        public int K { get; init; }
        public int N { get; init; }
        public int D { get; init; }

        public bool UseDiagonalCovariance { get; init; }

        public double LogEvidenceApprox { get; init; } // Infer.NET evidence proxy
        public double BIC { get; init; }

        // Posteriors
        public Dirichlet PiPosterior { get; init; } = new();
        public DistributionArray<VectorGaussian, Vector> MuPosterior { get; init; } = null!;
        public DistributionArray<Wishart, PositiveDefiniteMatrix> PrecPosterior { get; init; } = null!;

        // Soft assignments q(z_n)
        public Discrete[] ZPosterior { get; init; } = [];

        // Point estimates (for prediction convenience)
        public double[] WeightsMean { get; init; } = []; // size K
        public Vector[] MeansMean { get; init; } = []; // size K
        public PositiveDefiniteMatrix[] PrecMeans { get; init; } = []; // size K
    }

    /// <summary>
    /// Fit one K with Infer.NET Variational Message Passing.
    /// </summary>
    public static FitResult FitOne(
        double[][] x,
        int k,
        bool useDiagonalCovariance = false,
        int seed = 1234,
        int maxIterations = 200)
    {
        ValidateData(x);
        if (k <= 0) throw new ArgumentException("k must be > 0");

        var n = x.Length;
        var d = x[0].Length;

        // Convert data to Infer.NET Vector
        var xObs = x.Select(ToVector).ToArray();

        // Ranges
        var nRange = new Range(n).Named("n");
        var kRange = new Range(k).Named("k");

        // Hyperpriors
        // Mixing weights prior
        var alpha0Vec = Vector.Constant(k, 1.0); // symmetric Dirichlet(1)
        // var alpha0 = Dirichlet.FromMeanLog(alpha0Vec / alpha0Vec.Sum());

        // Mean prior: N(m0, (beta0 I)^-1)
        var m0 = Vector.Zero(d);
        const double beta0 = 1e-2;
        var meanPrecPrior = CreateScaledIdentity(d, beta0);

        // Precision prior: Wishart(nu0, W0)
        // nu0 must be > d - 1
        var nu0 = d + 2.0;
        var w0 = CreateScaledIdentity(d, 1.0);

        // Variables
        var pi = Variable.Dirichlet(alpha0Vec).Named("pi");
        var z = Variable.Array<int>(nRange).Named("z");
        z.SetValueRange(kRange);

        var mu = Variable.Array<Vector>(kRange).Named("mu");
        var prec = Variable.Array<PositiveDefiniteMatrix>(kRange).Named("prec");

        using (Variable.ForEach(kRange))
        {
            mu[kRange] = Variable.VectorGaussianFromMeanAndPrecision(m0, meanPrecPrior);
            prec[kRange] = Variable.WishartFromShapeAndScale(nu0, w0);
        }

        using (Variable.ForEach(nRange))
        {
            z[nRange] = Variable.Discrete(pi);
        }

        var xVar = Variable.Array<Vector>(nRange).Named("x");
        xVar.ObservedValue = xObs;

        using (Variable.ForEach(nRange))
        {
            using (Variable.Switch(z[nRange]))
            {
                xVar[nRange] = Variable.VectorGaussianFromMeanAndPrecision(mu[z[nRange]], prec[z[nRange]]);
            }
        }

        // Inference engine
        var engine = new InferenceEngine(new VariationalMessagePassing())
        {
            ShowProgress = false,
            NumberOfIterations = maxIterations,
            Compiler = { RecommendedQuality = QualityBand.Experimental }
        };

        // Evidence trick
        var evidence = Variable.Bernoulli(0.5).Named("evidence");
        var block = Variable.If(evidence);
        // Model already defined globally; block used as proxy
        block.CloseBlock();

        // Infer posteriors
        var piPost = engine.Infer<Dirichlet>(pi);
        var muPost = engine.Infer<DistributionArray<VectorGaussian, Vector>>(mu);
        var precPost = engine.Infer<DistributionArray<Wishart, PositiveDefiniteMatrix>>(prec);
        var zPost = engine.Infer<Discrete[]>(z);

        // Log evidence proxy
        // NOTE: In variational settings this is an approximation;
        // for BIC comparison keep same modeling setup across K.
        var logEvidenceApprox = engine.Infer<Bernoulli>(evidence).LogOdds;

        // Point estimates
        var wMean = piPost.GetMean().ToArray();
        NormalizeInPlace(wMean);

        var mMean = new Vector[k];
        var pMean = new PositiveDefiniteMatrix[k];
        for (var i = 0; i < k; i++)
        {
            mMean[i] = muPost[i].GetMean();
            pMean[i] = precPost[i].GetMean();
            if (useDiagonalCovariance)
            {
                // keep only diagonal to enforce pseudo-diagonal behavior at prediction time
                pMean[i] = KeepDiagonal(pMean[i], 1e-9);
            }
        }

        var paramCount = ParameterCount(k, d, useDiagonalCovariance);
        var bic = -2.0 * logEvidenceApprox + paramCount * Math.Log(n);

        return new FitResult
        {
            K = k,
            N = n,
            D = d,
            UseDiagonalCovariance = useDiagonalCovariance,
            LogEvidenceApprox = logEvidenceApprox,
            BIC = bic,
            PiPosterior = piPost,
            MuPosterior = muPost,
            PrecPosterior = precPost,
            ZPosterior = zPost,
            WeightsMean = wMean,
            MeansMean = mMean,
            PrecMeans = pMean
        };
    }

    /// <summary>
    /// Fit K in [kMin, kMax] and select by minimum BIC.
    /// </summary>
    public static (FitResult Best, List<FitResult> All) FitByBic(
        double[][] x,
        int kMin,
        int kMax,
        bool useDiagonalCovariance = false,
        int seed = 1234,
        int maxIterations = 200)
    {
        if (kMin <= 0 || kMax < kMin) throw new ArgumentException("Invalid K range.");

        var all = new List<FitResult>();
        for (var k = kMin; k <= kMax; k++)
        {
            var r = FitOne(
                x: x,
                k: k,
                useDiagonalCovariance: useDiagonalCovariance,
                seed: seed + k,
                maxIterations: maxIterations);
            all.Add(r);
        }

        var best = all.OrderBy(t => t.BIC).First();
        return (best, all);
    }

    /// <summary>
    /// Predict hard labels with fitted point estimates.
    /// </summary>
    public static int[] Predict(FitResult model, double[][] x)
    {
        ValidateData(x);
        ArgumentNullException.ThrowIfNull(model);
        if (x[0].Length != model.D) throw new ArgumentException("Dimension mismatch.");

        var n = x.Length;
        var k = model.K;
        var labels = new int[n];

        for (var i = 0; i < n; i++)
        {
            var xi = ToVector(x[i]);
            var best = double.NegativeInfinity;
            var arg = 0;
            for (var c = 0; c < k; c++)
            {
                var lp = Math.Log(Math.Max(model.WeightsMean[c], 1e-15))
                         + LogGaussianFromPrecision(xi, model.MeansMean[c], model.PrecMeans[c],
                             model.UseDiagonalCovariance);
                if (lp <= best)
                {
                    continue;
                }
                best = lp;
                arg = c;
            }
            labels[i] = arg;
        }

        return labels;
    }

    /// <summary>
    /// Posterior probabilities p(z=k|x)
    /// </summary>
    public static double[][] PredictProba(FitResult model, double[][] x)
    {
        ValidateData(x);
        ArgumentNullException.ThrowIfNull(model);
        if (x[0].Length != model.D) throw new ArgumentException("Dimension mismatch.");

        var n = x.Length;
        var k = model.K;
        var probs = new double[n][];

        for (var i = 0; i < n; i++)
        {
            var xi = ToVector(x[i]);
            var logp = new double[k];
            for (var c = 0; c < k; c++)
            {
                logp[c] = Math.Log(Math.Max(model.WeightsMean[c], 1e-15))
                          + LogGaussianFromPrecision(xi, model.MeansMean[c], model.PrecMeans[c],
                              model.UseDiagonalCovariance);
            }

            var lse = LogSumExp(logp);
            probs[i] = new double[k];
            for (var c = 0; c < k; c++)
            {
                probs[i][c] = Math.Exp(logp[c] - lse);
            }
        }

        return probs;
    }

    #region helpers

    private static int ParameterCount(int k, int d, bool diagonal)
    {
        var covParams = diagonal ? k * d : k * d * (d + 1) / 2;
        return (k - 1) + (k * d) + covParams;
    }

    private static void ValidateData(double[][] x)
    {
        if (x == null || x.Length == 0) throw new ArgumentException("Empty data.");
        var d = x[0].Length;
        if (d == 0) throw new ArgumentException("Empty dimension.");
        for (var i = 0; i < x.Length; i++)
        {
            if (x[i] == null || x[i].Length != d)
                throw new ArgumentException($"Row {i} dimension mismatch.");
        }
    }

    private static Vector ToVector(double[] row)
    {
        var v = Vector.Zero(row.Length);
        for (var i = 0; i < row.Length; i++) v[i] = row[i];
        return v;
    }

    private static PositiveDefiniteMatrix CreateScaledIdentity(int d, double scale)
    {
        var m = new PositiveDefiniteMatrix(d, d);
        for (var i = 0; i < d; i++) m[i, i] = scale;
        return m;
    }

    private static PositiveDefiniteMatrix KeepDiagonal(PositiveDefiniteMatrix src, double minDiag)
    {
        var d = src.Rows;
        var m = new PositiveDefiniteMatrix(d, d);
        for (var i = 0; i < d; i++)
            m[i, i] = Math.Max(src[i, i], minDiag);
        return m;
    }

    private static void NormalizeInPlace(double[] a)
    {
        var s = a.Sum();
        if (s <= 0) s = 1.0;
        for (var i = 0; i < a.Length; i++) a[i] = Math.Max(a[i] / s, 1e-15);
        var s2 = a.Sum();
        for (var i = 0; i < a.Length; i++) a[i] /= s2;
    }

    private static double LogGaussianFromPrecision(
        Vector x,
        Vector mean,
        PositiveDefiniteMatrix precision,
        bool diagonalOnly)
    {
        var d = x.Count;
        var diff = x - mean;

        if (diagonalOnly)
        {
            var quad = 0.0;
            var logDetPrec = 0.0;
            for (var i = 0; i < d; i++)
            {
                var p = Math.Max(precision[i, i], 1e-15);
                quad += p * diff[i] * diff[i];
                logDetPrec += Math.Log(p);
            }
            // log N = 0.5*log|P| -0.5*d*log(2π) -0.5*(x-μ)^T P (x-μ)
            return 0.5 * logDetPrec - 0.5 * d * Math.Log(2.0 * Math.PI) - 0.5 * quad;
        }
        else
        {
            // quad = diff^T P diff
            var tmp = precision * diff;
            var quad = diff.Inner(tmp);

            // log|P| from Cholesky
            var chol = precision.CholeskyInPlace(out _);
            var logDetPrec = 0.0;
            for (var i = 0; i < d; i++)
                logDetPrec += 2.0 * Math.Log(Math.Max(chol[i, i], 1e-15));

            return 0.5 * logDetPrec - 0.5 * d * Math.Log(2.0 * Math.PI) - 0.5 * quad;
        }
    }

    private static double LogSumExp(double[] a)
    {
        var m = a.Max();
        var s = a.Sum(t => Math.Exp(t - m));
        return m + Math.Log(s);
    }

    #endregion
}

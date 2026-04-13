using Microsoft.ML.Data;

namespace RecouvrementAPI.ML
{
    public class ScoringTrainingData
    {
        [LoadColumn(0)]
        public float RetardJours { get; set; }

        [LoadColumn(1)]
        public float NbEcheancesImpayees { get; set; }

        [LoadColumn(2)]
        public float HasGarantie { get; set; } // 0 or 1

        [LoadColumn(3)]
        public float GarantieForte { get; set; } // 0 or 1 (hypotheque/salaire)

        [LoadColumn(4)]
        public float MontantImpaye { get; set; }

        [LoadColumn(5)]
        public float HasIntention { get; set; } // 0 or 1

        [LoadColumn(6)]
        public float IntentionPositive { get; set; } // 0 or 1

        [LoadColumn(7)]
        public string Niveau { get; set; } // Label (Faible, Moyen, Élevé)
    }
}

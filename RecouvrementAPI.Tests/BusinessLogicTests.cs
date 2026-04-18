using System;
using System.Collections.Generic;
using RecouvrementAPI.Helpers;
using RecouvrementAPI.Models;
using Xunit;

namespace RecouvrementAPI.Tests
{
    public class BusinessLogicTests
    {
        [Fact]
        public void CalculerJoursRetard_ShouldReturnZero_WhenNoImpaye()
        {
            var echeances = new List<Echeance>
            {
                new Echeance { Statut = "paye", DateEcheance = DateTime.UtcNow.AddDays(-10) }
            };

            int result = RecouvrementHelper.CalculerJoursRetard(echeances);

            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculerJoursRetard_ShouldReturnCorrectDays_WhenImpayeExists()
        {
            var fiveDaysAgo = DateTime.UtcNow.AddDays(-5);
            var echeances = new List<Echeance>
            {
                new Echeance { Statut = "impaye", DateEcheance = fiveDaysAgo }
            };

            int result = RecouvrementHelper.CalculerJoursRetard(echeances);

            Assert.Equal(5, result);
        }

     

        [Fact]
        public void CalculerJoursRetard_EmptyList_ShouldReturnZero()
        {
            var result = RecouvrementHelper.CalculerJoursRetard(new List<Echeance>());
            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculerJoursRetard_Null_ShouldReturnZero()
        {
            var result = RecouvrementHelper.CalculerJoursRetard(null);
            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculerInteretsRetard_ShouldReturnZero_WhenRetardUnder90Days()
        {
            decimal principal = 1000m;
            decimal taux = 5m;
            int jours = 45;

            decimal result = RecouvrementHelper.CalculerInteretsRetard(principal, taux, jours);

            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculerInteretsRetard_ShouldApplyFormula_WhenRetardOver90Days()
        {
            decimal principal = 1000m;
            decimal taux = 10m;
            int jours = 365;

            decimal result = RecouvrementHelper.CalculerInteretsRetard(principal, taux, jours);

            Assert.Equal(100.000m, result);
        }

        [Fact]
        public void CalculerInteretsRetard_ShouldRoundTo3Decimals()
        {
            decimal principal = 1234.567m;
            decimal taux = 7.5m;
            int jours = 120;

            decimal result = RecouvrementHelper.CalculerInteretsRetard(principal, taux, jours);

            Assert.Equal(30.441m, result);
        }
    }
}

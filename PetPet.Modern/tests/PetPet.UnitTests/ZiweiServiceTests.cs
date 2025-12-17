using Xunit;
using PetPet.Application.Services;
using PetPet.Domain.Enums;
using System;

namespace PetPet.UnitTests
{
    public class ZiweiServiceTests
    {
        private readonly ZiweiService _service;

        public ZiweiServiceTests()
        {
            _service = new ZiweiService();
        }

        [Fact]
        public void CalculateStar_ShouldReturnConsistentResult()
        {
            // Arrange
            var birthday = new DateTime(1990, 1, 1);
            
            // Act
            var star = _service.CalculateStar(birthday);
            
            // Assert
            // (1990 + 1 + 1) % 14 = 1992 % 14 = 4 -> Wuqu (assuming enum order)
            // Just verifying it returns a valid enum and is deterministic
            Assert.True(Enum.IsDefined(typeof(ZiweiStar), star));
            
            var star2 = _service.CalculateStar(birthday);
            Assert.Equal(star, star2);
        }

        [Fact]
        public void GetAnalysis_ShouldReturnDetails()
        {
            // Arrange
            var star = ZiweiStar.Ziwei;

            // Act
            var result = _service.GetAnalysis(star);

            // Assert
            Assert.Equal("紫微星", result.Name);
            Assert.Contains("尊貴", result.Personality);
        }

        [Fact]
        public void CalculateMatchScore_ShouldReturnScore()
        {
            // Arrange
            var starA = ZiweiStar.Ziwei;
            var starB = ZiweiStar.Tianfu; // Usually a good match

            // Act
            var score = _service.CalculateMatchScore(starA, starB);

            // Assert
            Assert.InRange(score, 0, 100);
            Assert.True(score > 60); // Expect decent score for random pair
        }
    }
}

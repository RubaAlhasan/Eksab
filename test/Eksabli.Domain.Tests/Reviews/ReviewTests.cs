using System;
using Shouldly;
using Xunit;

namespace Eksabli.Reviews;

public class ReviewTests
{
    [Fact]
    public void Create_Sets_The_Rating_And_Comment()
    {
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), 4, "Great service.");

        review.Rating.ShouldBe(4);
        review.Comment.ShouldBe("Great service.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_Rejects_A_Rating_Outside_One_To_Five(int rating)
    {
        Should.Throw<ArgumentException>(() => Review.Create(Guid.NewGuid(), Guid.NewGuid(), rating, null));
    }

    [Fact]
    public void SetRating_Replaces_The_Rating_And_Comment_In_Place()
    {
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), 2, "Not great.");

        review.SetRating(5, "Changed my mind, it's excellent.");

        review.Rating.ShouldBe(5);
        review.Comment.ShouldBe("Changed my mind, it's excellent.");
    }
}

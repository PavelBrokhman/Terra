namespace Terra.Node.Tests;

public class QuotaTests
{
    [Fact]
    public void Starts_Empty()
    {
        var quota = new Quota(10);

        Assert.Equal(10, quota.Total);
        Assert.Equal(0, quota.Used);
        Assert.Equal(10, quota.Remaining);
    }

    [Fact]
    public void Take_Consumes_Room()
    {
        var quota = new Quota(10);

        Assert.True(quota.TryTake(4));
        Assert.Equal(6, quota.Remaining);
    }

    [Fact]
    public void Take_Refuses_To_Overdraw_And_Changes_Nothing()
    {
        var quota = new Quota(10);
        quota.TryTake(8);

        Assert.False(quota.TryTake(3));
        Assert.Equal(8, quota.Used);
    }

    [Fact]
    public void Take_Right_Up_To_The_Limit_Is_Allowed()
    {
        var quota = new Quota(10);

        Assert.True(quota.TryTake(10));
        Assert.Equal(0, quota.Remaining);
    }

    [Fact]
    public void Release_Gives_Room_Back_And_Never_Goes_Negative()
    {
        var quota = new Quota(10);
        quota.TryTake(3);

        quota.Release(5);

        Assert.Equal(0, quota.Used);
    }

    [Fact]
    public void ReleaseAll_Returns_Everything()
    {
        var quota = new Quota(10);
        quota.TryTake(7);

        quota.ReleaseAll();

        Assert.Equal(10, quota.Remaining);
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Zarn.Tests.TestTypes;

namespace Zarn.Tests;

public sealed class ExceptionPropagationTests : RpcTestsBase
{
    private static Task SimpleTest(Type type, object?[] args, Action<Exception> assert)
    {
        var localEx = (Exception?)Activator.CreateInstance(type, args);
        Assert.NotNull(localEx);

        return RunConnectToServerTest<IThrower, Thrower>(async x =>
        {
            var syncThrown = Assert.Throws(type, () => x.Throw(type, args));
            CheckException(syncThrown);

            var asyncThrown = await Assert.ThrowsAsync(type, () => x.ThrowAsync(type, args));
            CheckException(asyncThrown);
        });

        void CheckException(Exception e)
        {
            Assert.Equal(localEx.Message, e.Message);
            Assert.Null(e.InnerException);
            Assert.Contains("at Zarn.Tests.TestTypes.Thrower.Throw", e.StackTrace);
            assert(e);
        }
    }

    private static Task SimpleTest<T>(Expression<Func<T>> expression) where T : Exception 
        => SimpleTest(expression, e => { });

    private static Task SimpleTest<T>(Expression<Func<T>> expression, Action<T> assert) where T : Exception
    {
        var ctorCall = (NewExpression)expression.Body;
        var args = ctorCall.Arguments.Select(GetLiteralValue).ToArray();
        return SimpleTest(typeof(T), args, e => assert((T)e));
    }

    [ExcludeFromCodeCoverage]
    private static object? GetLiteralValue(Expression expression)
    {
        return expression switch
        {
            ConstantExpression c => c.Value,
            UnaryExpression { NodeType: ExpressionType.Convert } u => GetLiteralValue(u.Operand),
            _ => throw new NotImplementedException(expression.ToString()),
        };
    }

    [Fact]
    public Task Ex()
        => SimpleTest(() => new Exception());

    [Fact]
    public Task ExWithMessage()
        => SimpleTest(() => new Exception("some message"));

    [Fact]
    public Task InvalidOp()
        => SimpleTest(() => new InvalidOperationException());

    [Fact]
    public Task InvalidOpWithMessage()
        => SimpleTest(() => new InvalidOperationException("some message"));

    [Fact]
    public Task Arg()
        => SimpleTest(() => new ArgumentException(), e =>
        {
            Assert.Null(e.ParamName);
        });

    [Fact]
    public Task ArgWithMessage()
        => SimpleTest(() => new ArgumentException("some message"), e =>
        {
            Assert.Null(e.ParamName);
        });

    [Fact]
    public Task ArgWithParam()
        => SimpleTest(() => new ArgumentException(null, "someParam"), e =>
        {
            Assert.Equal("someParam", e.ParamName);
        });

    [Fact]
    public Task ArgWithMessageAndParam()
        => SimpleTest(() => new ArgumentException("some message", "someParam"), e =>
        {
            Assert.Equal("someParam", e.ParamName);
        });

    [Fact]
    public Task ArgNull()
        => SimpleTest(() => new ArgumentNullException(), e =>
        {
            Assert.Null(e.ParamName);
        });

    [Fact]
    public Task ArgNullWithParam()
        => SimpleTest(() => new ArgumentNullException("someParam"), e =>
        {
            Assert.Equal("someParam", e.ParamName);
        });

    [Fact]
    public Task ArgNullWithMessage()
        => SimpleTest(() => new ArgumentNullException(null, "some message"), e =>
        {
            Assert.Null(e.ParamName);
        });

    [Fact]
    public Task ArgNullWithMessageAndParam()
        => SimpleTest(() => new ArgumentNullException("someParam", "some message"), e =>
        {
            Assert.Equal("someParam", e.ParamName);
        });

    [Fact]
    public Task ArgOor()
        => SimpleTest(() => new ArgumentOutOfRangeException(), e =>
        {
            Assert.Null(e.ParamName);
        });

    [Fact]
    public Task ArgOorWithParam()
        => SimpleTest(() => new ArgumentOutOfRangeException("someParam"), e =>
        {
            Assert.Equal("someParam", e.ParamName);
            Assert.Null(e.ActualValue);
        });

    [Fact]
    public Task ArgOorWithParamAndMessage()
        => SimpleTest(() => new ArgumentOutOfRangeException("someParam", "some message"), e =>
        {
            Assert.Equal("someParam", e.ParamName);
            Assert.Null(e.ActualValue);
        });

    [Fact]
    public Task ArgOorWithParamAndMessageAndIntValue()
        => SimpleTest(() => new ArgumentOutOfRangeException("someParam", 1, "some message"), e =>
        {
            Assert.Equal("someParam", e.ParamName);
            Assert.Equal(1, e.ActualValue);
        });

    [Fact]
    public Task ArgOorWithParamAndMessageAndStringValue()
        => SimpleTest(() => new ArgumentOutOfRangeException("someParam", "param value", "some message"), e =>
        {
            Assert.Equal("someParam", e.ParamName);
            Assert.Equal("param value", e.ActualValue);
        });
}

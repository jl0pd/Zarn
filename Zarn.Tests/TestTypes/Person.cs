using System.Buffers;
using Zarn.Serialization;

namespace Zarn.Tests.TestTypes;

public sealed record Person(string Name, int Age);

public sealed class PersonBinarySerializer : BinarySerializer<Person>
{
    public override Person Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new Person(
            context.Deserialize<string>(ref source),
            context.Deserialize<int>(ref source)
        );
    }

    public override void Serialize(Person value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.Name, writer);
        context.Serialize(value.Age, writer);
    }
}

public interface IPersonFactory
{
    public ValueTask<Person> CreatePerson(string name, int age);
}

public sealed class PersonFactory : IPersonFactory
{
    public ValueTask<Person> CreatePerson(string name, int age)
    {
        return new ValueTask<Person>(new Person(name, age));
    }
}

using System.Text;

namespace ProjectPricing.Api.Idempotencia;

/// <summary>Repassa tudo que é escrito ao fluxo original (o SSE segue em tempo real) e guarda uma cópia.</summary>
public sealed class FluxoEspelho(Stream destino) : Stream
{
    private readonly MemoryStream _copia = new();

    public long TotalGravado => _copia.Length;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public string Texto() => Encoding.UTF8.GetString(_copia.GetBuffer(), 0, (int)_copia.Length);

    public override void Write(byte[] buffer, int offset, int count)
    {
        _copia.Write(buffer, offset, count);
        destino.Write(buffer, offset, count);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _copia.Write(buffer.Span);
        await destino.WriteAsync(buffer, cancellationToken);
    }

    public override void Flush() => destino.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => destino.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _copia.Dispose();
        }

        base.Dispose(disposing);
    }
}

import { readFixedString, writeFixedString } from "./string-util.ts";

export class PacketWriter {
  private chunks: Uint8Array[] = [];
  private _size = 0;

  get size(): number {
    return this._size;
  }

  writeUint8(value: number): this {
    const buffer = new Uint8Array(1);
    buffer[0] = value & 0xff;
    this.chunks.push(buffer);
    this._size += 1;
    return this;
  }

  writeUint16(value: number): this {
    const buffer = new Uint8Array(2);
    new DataView(buffer.buffer).setUint16(0, value, false);
    this.chunks.push(buffer);
    this._size += 2;
    return this;
  }

  writeUint32(value: number): this {
    const buffer = new Uint8Array(4);
    new DataView(buffer.buffer).setUint32(0, value, false);
    this.chunks.push(buffer);
    this._size += 4;
    return this;
  }

  writeInt8(value: number): this {
    const buffer = new Uint8Array(1);
    new DataView(buffer.buffer).setInt8(0, value);
    this.chunks.push(buffer);
    this._size += 1;
    return this;
  }

  writeInt32(value: number): this {
    const buffer = new Uint8Array(4);
    new DataView(buffer.buffer).setInt32(0, value, false);
    this.chunks.push(buffer);
    this._size += 4;
    return this;
  }

  writeBytes(bytes: Uint8Array): this {
    this.chunks.push(bytes.slice());
    this._size += bytes.length;
    return this;
  }

  writeFixedString(value: string, length: number): this {
    this.chunks.push(writeFixedString(value, length));
    this._size += length;
    return this;
  }

  writePadding(length: number): this {
    this.chunks.push(new Uint8Array(length));
    this._size += length;
    return this;
  }

  build(): Uint8Array {
    const result = new Uint8Array(this._size);
    let offset = 0;
    for (const chunk of this.chunks) {
      result.set(chunk, offset);
      offset += chunk.length;
    }
    return result;
  }
}

export class PacketReader {
  private offset = 0;

  constructor(private buffer: Uint8Array) { }

  get position(): number {
    return this.offset;
  }

  remaining(): number {
    return this.buffer.length - this.offset;
  }

  readUint8(): number {
    const value = this.buffer[this.offset];
    this.offset += 1;
    return value;
  }

  readUint16(): number {
    const value = new DataView(this.buffer.buffer, this.buffer.byteOffset + this.offset, 2).getUint16(0, false);
    this.offset += 2;
    return value;
  }

  readUint32(): number {
    const value = new DataView(this.buffer.buffer, this.buffer.byteOffset + this.offset, 4).getUint32(0, false);
    this.offset += 4;
    return value;
  }

  readInt32(): number {
    const value = new DataView(this.buffer.buffer, this.buffer.byteOffset + this.offset, 4).getInt32(0, false);
    this.offset += 4;
    return value;
  }

  readInt8(): number {
    const value = new DataView(this.buffer.buffer, this.buffer.byteOffset + this.offset, 1).getInt8(0);
    this.offset += 1;
    return value;
  }

  readBytes(length: number): Uint8Array {
    const slice = this.buffer.slice(this.offset, this.offset + length);
    this.offset += length;
    return slice;
  }

  readFixedString(maxLength: number): string {
    const value = readFixedString(this.buffer, this.offset, maxLength);
    this.offset += maxLength;
    return value;
  }

  skip(length: number): this {
    this.offset += length;
    return this;
  }
}

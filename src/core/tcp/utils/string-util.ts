const ENCODER = new TextEncoder();
const DECODER = new TextDecoder("iso-8859-1");

export function readFixedString(buffer: Uint8Array, offset: number, maxLength: number): string {
  const slice = buffer.slice(offset, offset + maxLength);
  const nullIndex = slice.indexOf(0);
  return DECODER.decode(nullIndex >= 0 ? slice.slice(0, nullIndex) : slice);
}

export function writeFixedString(value: string, maxLength: number): Uint8Array {
  const bytes = new Uint8Array(maxLength);
  const encoded = ENCODER.encode(value).slice(0, maxLength);
  bytes.set(encoded);
  return bytes;
}

export function isValidName(name: string): boolean {
  if (!name || name[0] === " " || name[name.length - 1] === " ") return false;
  return /^[\x21-\x7e\xa1-\xff ]+$/.test(name);
}

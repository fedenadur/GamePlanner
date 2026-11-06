import { parseHours } from './hours';

describe('parseHours', () => {
  it('accepts comma and dot decimals and trims', () => {
    expect(parseHours('1,5')).toBe(1.5);
    expect(parseHours('1.5')).toBe(1.5);
    expect(parseHours(' 2 ')).toBe(2);
  });

  it('accepts the range boundaries', () => {
    expect(parseHours('0.1')).toBe(0.1);
    expect(parseHours('24')).toBe(24);
  });

  it('rejects out of range and non numeric input', () => {
    expect(parseHours('0')).toBeNull();
    expect(parseHours('24.5')).toBeNull();
    expect(parseHours('abc')).toBeNull();
    expect(parseHours('')).toBeNull();
    expect(parseHours('1.2.3')).toBeNull();
    expect(parseHours('0.09')).toBeNull();
  });
});

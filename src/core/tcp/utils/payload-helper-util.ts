export function forEachPage<T>(
  elements: T[],
  maxPerPage: number,
  consumer: (pageElements: T[]) => void
): void {
  const full = Math.floor(elements.length / maxPerPage);
  const partial = elements.length % maxPerPage;

  const hasPartial = partial > 0;
  const total = full + (hasPartial ? 1 : 0);

  let elem = 0;
  for (let i = 0; i < total; i++) {
    let elems: number;
    if (!hasPartial || i < total - 1) {
      elems = maxPerPage;
    } else {
      elems = partial;
    }

    const pageElements: T[] = [];
    for (let j = 0; j < elems; j++) {
      pageElements.push(elements[elem++]);
    }
    consumer(pageElements);
  }
}
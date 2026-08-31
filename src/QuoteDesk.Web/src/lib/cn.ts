/** Join class names, dropping falsy entries. The whole of what a `clsx` dependency would give us. */
export function cn(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ')
}

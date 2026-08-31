/**
 * The trace panel never shows a raw tool or stage identifier — those are internal. Every step is
 * labelled with what it did, in plain language. An unmapped name degrades to a de-underscored,
 * title-cased form rather than leaking the identifier verbatim.
 */

const TOOL_LABELS: Record<string, string> = {
  resolve_customer: 'Matched customer',
  search_catalog: 'Searched catalogue',
  get_customer_history: 'Checked order history',
  check_stock: 'Checked stock',
  price_quote: 'Priced the quote',
  create_quote_draft: 'Created draft quote',
  send_quote: 'Sent quote',
}

const STAGE_LABELS: Record<string, string> = {
  extract: 'Read the enquiry',
  resolve: 'Resolved items & stock',
  price: 'Priced the quote',
}

function titleCase(identifier: string): string {
  const words = identifier.replace(/[_-]+/g, ' ').trim()
  return words.length === 0 ? identifier : words.charAt(0).toUpperCase() + words.slice(1)
}

export function toolLabel(name: string): string {
  return TOOL_LABELS[name] ?? titleCase(name)
}

export function stageLabel(stage: string): string {
  return STAGE_LABELS[stage] ?? titleCase(stage)
}

/** Short badge text for the stage column. */
export function stageBadge(stage: string): string {
  switch (stage) {
    case 'extract':
      return 'Extract'
    case 'resolve':
      return 'Resolve'
    case 'price':
      return 'Price'
    default:
      return titleCase(stage)
  }
}

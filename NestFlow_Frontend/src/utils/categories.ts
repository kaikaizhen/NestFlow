/**
 * 分類代碼對應的顯示樣式。分類清單本身由後端提供，此處只補上圖示與色彩。
 */
export interface CategoryStyle {
  /** SVG path 內容，搭配 24x24 viewBox 使用。 */
  path: string
  /** 圖示底色。 */
  tint: string
}

const styles: Record<string, CategoryStyle> = {
  food: {
    path: 'M7 3v8a2 2 0 0 0 2 2v8M7 3v6M10 3v6M17 3c-1.5 2-2 4-2 6s.5 3 2 3v9',
    tint: 'var(--color-expense-tint)',
  },
  transport: {
    path: 'M5 16V7a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v9M5 16h14M5 16v2M19 16v2M8 10h8M8 13h.01M16 13h.01',
    tint: 'var(--color-expense-tint)',
  },
  shopping: {
    path: 'M4 7h16l-1.4 11.2a2 2 0 0 1-2 1.8H7.4a2 2 0 0 1-2-1.8L4 7ZM9 7V5.5a3 3 0 0 1 6 0V7',
    tint: 'var(--color-expense-tint)',
  },
  home: {
    path: 'M4 11 12 4l8 7M6 10v9h12v-9',
    tint: 'var(--color-expense-tint)',
  },
  medical: {
    path: 'M12 6v12M6 12h12',
    tint: 'var(--color-expense-tint)',
  },
  entertainment: {
    path: 'M9 18V6l10-2v12M9 18a2.5 2.5 0 1 1-5 0 2.5 2.5 0 0 1 5 0Zm10-2a2.5 2.5 0 1 1-5 0 2.5 2.5 0 0 1 5 0Z',
    tint: 'var(--color-expense-tint)',
  },
  other_expense: {
    path: 'M5 12h.01M12 12h.01M19 12h.01',
    tint: 'var(--color-neutral-tint)',
  },
  salary: {
    path: 'M4 7h16v12H4zM4 7l2-3h12l2 3M9 13h6',
    tint: 'var(--color-income-tint)',
  },
  bonus: {
    path: 'M4 9h16v11H4zM4 9l1.5-4h13L20 9M12 9v11M9 5a2 2 0 1 1 3 1.6M15 5a2 2 0 1 0-3 1.6',
    tint: 'var(--color-income-tint)',
  },
  investment: {
    path: 'M4 18l5-6 4 3.5L20 7M20 7h-4M20 7v4',
    tint: 'var(--color-income-tint)',
  },
  other_income: {
    path: 'M5 12h.01M12 12h.01M19 12h.01',
    tint: 'var(--color-income-tint)',
  },
}

const fallback: CategoryStyle = {
  path: 'M5 12h.01M12 12h.01M19 12h.01',
  tint: 'var(--color-neutral-tint)',
}

export function categoryStyle(code: string): CategoryStyle {
  return styles[code] ?? fallback
}

import { describe, expect, it } from 'vitest';
import { categoryIcon } from './category-display.util';

describe('categoryIcon', () => {
  // The real, seeded taxonomy (CategoryDataSeederContributor on the backend) — every one of these must
  // resolve to something other than the generic fallback.
  it('matches every seeded category to its own icon', () => {
    expect(categoryIcon('Restaurants & Cafes')).toBe('fa-utensils');
    expect(categoryIcon('Retail & Shopping')).toBe('fa-bag-shopping');
    expect(categoryIcon('Beauty & Wellness')).toBe('fa-spa');
    expect(categoryIcon('Health & Fitness')).toBe('fa-dumbbell');
    expect(categoryIcon('Entertainment')).toBe('fa-film');
    expect(categoryIcon('Services')).toBe('fa-screwdriver-wrench');
    expect(categoryIcon('Education')).toBe('fa-graduation-cap');
    expect(categoryIcon('Travel & Hospitality')).toBe('fa-plane');
  });

  it('matches case-insensitively and on a single keyword, for a category an admin renames or adds later', () => {
    expect(categoryIcon('cafe')).toBe('fa-utensils');
    expect(categoryIcon('GYM')).toBe('fa-dumbbell');
  });

  it('falls back to a generic icon for anything unrecognized, instead of leaving it blank', () => {
    expect(categoryIcon('Something Else Entirely')).toBe('fa-tag');
    expect(categoryIcon(null)).toBe('fa-tag');
    expect(categoryIcon(undefined)).toBe('fa-tag');
  });
});

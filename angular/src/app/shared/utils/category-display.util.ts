// Categories have no icon of their own in practice: `Category.IconBlobName` exists as a column, but
// no upload endpoint or serving route was ever built for it (unlike BusinessProfile's real LogoBlobName
// pipeline — confirmed by reading CategoryAppService) and the admin form only ever let someone type a
// blob name by hand. Nothing in this codebase has ever set it to a real value, so it is not safe to
// render as an image today.
//
// This instead keys off the category's own English name, matched by keyword rather than by the seeded
// list's eight exact strings — a category is a plain admin-editable row, not a fixed enum like
// CampaignType, so a name an admin tweaks later (or a new one they add) should still get a sensible
// icon instead of silently falling through to the generic one.
const ICON_BY_KEYWORD: readonly [RegExp, string][] = [
  [/restaurant|cafe|caf[eé]|food|dining/i, 'fa-utensils'],
  [/retail|shop|store/i, 'fa-bag-shopping'],
  [/beauty|wellness|spa/i, 'fa-spa'],
  [/health|fitness|gym/i, 'fa-dumbbell'],
  [/entertain/i, 'fa-film'],
  [/service/i, 'fa-screwdriver-wrench'],
  [/education|learn|school/i, 'fa-graduation-cap'],
  [/travel|hospitality|hotel/i, 'fa-plane'],
];

const FALLBACK_ICON = 'fa-tag';

export function categoryIcon(nameEn: string | null | undefined): string {
  if (nameEn) {
    for (const [pattern, icon] of ICON_BY_KEYWORD) {
      if (pattern.test(nameEn)) return icon;
    }
  }
  return FALLBACK_ICON;
}

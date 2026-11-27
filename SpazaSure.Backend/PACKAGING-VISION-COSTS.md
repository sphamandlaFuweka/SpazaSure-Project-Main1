# Packaging photo analysis cost controls

Text recognition remains on-device with Google ML Kit. Photo-based analysis now uses Claude Sonnet 5.5 for the first pass and escalates to Claude Opus 5.5 when the image is not rated good, the verdict is uncertain or concerning, or the first pass reports red flags. When the two models disagree, the returned result keeps the more cautious verdict.

The output-token ceiling is 8,192 (half the previous ceiling, while leaving room for long ingredient lists). Each provider response writes a structured backend log with the attempt, actual served model, input/output token counts, and an estimated USD cost. The estimate uses the current configured-in-code list prices ($2/$10 per million input/output tokens for Sonnet 5.5 and $4/$20 for Opus 5.5); update those rates when Anthropic changes pricing. Logs do not include the image or recognized label text.

The existing limit of 40 successful packaging analyses per user per day remains. This change does not add a global monthly spend cap: set a spend limit in the Anthropic Console if a hard account-level limit is required. The same Anthropic account may also be used for compliance-document analysis, so its account-wide spend includes those requests.

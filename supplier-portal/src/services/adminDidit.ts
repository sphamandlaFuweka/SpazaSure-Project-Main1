import toast from 'react-hot-toast';
import api, { apiErrorMessage } from './api';

export type DiditSubjectType = 'shop' | 'supplier';

const POLL_EVERY_MS = 4000;
const GIVE_UP_AFTER_MS = 4 * 60 * 1000;

/**
 * Runs a verification check that completes on the server; nothing opens.
 * - Supplier: CIPC register lookup (registration number, company still active) and sanctions screening.
 * - Shop: the admin confirms they have seen the owner's original ID in person, then the owner is
 *   screened against sanctions lists.
 * Follows the check until it finishes and returns the final status (Approved, In Review, Declined),
 * or null if it was cancelled or couldn't run.
 */
export async function openAdminDiditCheck(type: DiditSubjectType, id: string, name: string): Promise<string | null> {
  const confirmed = type === 'shop'
    ? window.confirm(
      `Verify the owner of ${name} in person?\n\n` +
      `Only continue if you have seen the owner's ORIGINAL ID document yourself, and the name and ID number ` +
      `on it match the shop's profile.\n\nSpazaSure will then screen the owner against sanctions lists and complete the check.`)
    : window.confirm(
      `Run the business check for ${name}?\n\n` +
      `SpazaSure looks the company up on the CIPC register (registration number and status) and screens it ` +
      `against sanctions lists. It takes up to a minute or two.`);
  if (!confirmed) return null;

  try {
    await api.post(`/compliance/identity/admin/${type}/${id}/check`, type === 'shop' ? { idSeenInPerson: true } : {});
  } catch (error) {
    toast.error(apiErrorMessage(error, 'Could not start the check'));
    return null;
  }

  const progress = toast.loading(`Checking ${name}…`);
  const started = Date.now();
  while (Date.now() - started < GIVE_UP_AFTER_MS) {
    await new Promise((resolve) => setTimeout(resolve, POLL_EVERY_MS));
    try {
      const { data } = await api.get(`/compliance/identity/admin/${type}/${id}`);
      const check = data?.data as { status: string; reason: string | null } | undefined;
      if (!check || check.status === 'In Progress') continue;
      toast.dismiss(progress);
      if (check.status === 'Approved') toast.success(`${name} passed the check.`);
      else if (check.status === 'In Review') toast(`${name} needs a closer look: see the Didit notes.`, { icon: '⚠️' });
      else if (check.status === 'Declined') toast.error(`${name} did not pass: ${check.reason ?? 'see the Didit notes.'}`);
      else toast.error(check.reason ?? 'The check could not be completed. Try again.');
      return check.status;
    } catch {
      // Keep following; a single failed poll isn't the end of the check.
    }
  }
  toast.dismiss(progress);
  toast(`The check for ${name} is still running. Refresh in a minute to see the result.`);
  return 'In Progress';
}

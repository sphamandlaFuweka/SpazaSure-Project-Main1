import toast from 'react-hot-toast';
import api, { apiErrorMessage } from './api';

export type DiditSubjectType = 'shop' | 'supplier';

export async function openAdminDiditCheck(type: DiditSubjectType, id: string, name: string): Promise<boolean> {
  const person = type === 'shop' ? 'shop owner' : 'supplier';
  if (!window.confirm(
    `Start an admin-supervised Didit check for ${name}?\n\n` +
    `Make sure the ${person} is physically present. You will supervise the check; ` +
    `they must provide their own identity details and complete any selfie or liveness steps.`
  )) {
    return false;
  }

  const verificationWindow = window.open('about:blank', '_blank');
  if (!verificationWindow) {
    toast.error('Allow pop-ups for this site to open Didit verification.');
    return false;
  }
  verificationWindow.opener = null;

  try {
    const response = await api.post(`/compliance/identity/admin/${type}/${id}/session`);
    const url = response.data?.data?.url;
    if (typeof url !== 'string') throw new Error('Didit did not return a verification link.');

    const diditUrl = new URL(url);
    if (diditUrl.protocol !== 'https:' || !diditUrl.hostname.endsWith('.didit.me')) {
      throw new Error('Didit returned an invalid verification link.');
    }
    verificationWindow.location.replace(diditUrl.href);
    toast.success(`Didit opened for ${name}. Supervise the check while they complete their own identity steps.`);
    return true;
  } catch (error) {
    verificationWindow.close();
    toast.error(apiErrorMessage(error, 'Could not start Didit verification'));
    return false;
  }
}

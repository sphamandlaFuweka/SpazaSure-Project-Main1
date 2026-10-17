import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Copy, KeyRound, Loader2, Pencil, ShieldCheck, UserPlus, UserX, UserCheck } from 'lucide-react';
import clsx from 'clsx';
import { Modal } from '../../components/ui';
import { adminTeamApi, type TeamMember } from '../../services/api';
import { useAuthStore } from '../../store/authStore';
import {
  ADMIN_AREAS, ROLE_PRESETS, fullAccessMap,
  type AccessLevel, type AdminArea,
} from '../../utils/adminAccess';

const LEVELS: { value: AccessLevel; label: string }[] = [
  { value: 'none', label: 'No access' },
  { value: 'view', label: 'View' },
  { value: 'manage', label: 'Manage' },
];

const levelChip: Record<AccessLevel, string> = {
  none: 'bg-gray-100 text-gray-400',
  view: 'bg-brand-blue-light text-brand-blue',
  manage: 'bg-emerald-50 text-emerald-700',
};

const formatDate = (value?: string | null) =>
  value ? new Date(value).toLocaleDateString('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' }) : 'Never';

/** Master admin only: register the people who use the admin console and decide what each can reach. */
export default function AdminTeamPage() {
  const { adminProfile } = useAuthStore();
  const [members, setMembers] = useState<TeamMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<TeamMember | 'new' | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [password, setPassword] = useState<{ name: string; email: string; value: string } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setMembers((await adminTeamApi.list()).members ?? []);
    } catch {
      setMembers([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const counts = useMemo(() => ({
    masters: members.filter((m) => m.isMaster && m.active).length,
    active: members.filter((m) => m.active).length,
    suspended: members.filter((m) => !m.active).length,
  }), [members]);

  const toggleActive = async (member: TeamMember) => {
    if (!window.confirm(member.active
      ? `Suspend ${member.displayName || member.email}? They are signed out at once and can't use the admin console until restored.`
      : `Restore access for ${member.displayName || member.email}?`)) return;
    setBusyId(member.id);
    try {
      await adminTeamApi.setActive(member.id, !member.active);
      toast.success(member.active ? 'Access suspended' : 'Access restored');
      await load();
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setBusyId(null);
    }
  };

  const resetPassword = async (member: TeamMember) => {
    if (!window.confirm(`Create a new temporary password for ${member.displayName || member.email}? Their current password stops working and they are signed out.`)) return;
    setBusyId(member.id);
    try {
      const { temporaryPassword } = await adminTeamApi.resetPassword(member.id);
      setPassword({ name: member.displayName || member.email, email: member.email, value: temporaryPassword });
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setBusyId(null);
    }
  };

  if (adminProfile && !adminProfile.isMaster) {
    return (
      <div className="p-6">
        <div className="card max-w-lg mx-auto mt-10 p-8 text-center">
          <ShieldCheck size={28} className="mx-auto text-brand-blue" />
          <h1 className="mt-3 text-lg font-black text-gray-900">Only master admins manage the team</h1>
          <p className="mt-2 text-sm text-ink-3">Ask a master admin to change who can use the admin console.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 space-y-6 animate-in">
      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
        <div>
          <h1 className="page-title">Team &amp; Access</h1>
          <p className="page-subtitle">
            Register the people who use the admin console. Give each person a role, then choose which areas they can view or manage.
          </p>
        </div>
        <button className="btn-primary flex items-center gap-2 flex-shrink-0" onClick={() => setEditing('new')}>
          <UserPlus size={16} /> Add team member
        </button>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <Stat label="Active team members" value={counts.active} />
        <Stat label="Master admins" value={counts.masters} />
        <Stat label="Suspended" value={counts.suspended} />
      </div>

      <div className="card overflow-hidden">
        {loading ? (
          <div className="flex justify-center py-16"><Loader2 className="animate-spin text-brand-blue" /></div>
        ) : members.length === 0 ? (
          <p className="py-16 text-center text-sm text-ink-3">No team members yet.</p>
        ) : (
          <div className="divide-y divide-brand-blue-light">
            {members.map((member) => (
              <div key={member.id} className={clsx('p-4 sm:p-5 flex flex-col lg:flex-row lg:items-center gap-4', !member.active && 'opacity-60')}>
                <div className="flex items-center gap-3 min-w-0 lg:w-72 flex-shrink-0">
                  <div className="w-10 h-10 rounded-full bg-brand-blue text-white flex items-center justify-center font-black flex-shrink-0">
                    {(member.displayName || member.email).charAt(0).toUpperCase()}
                  </div>
                  <div className="min-w-0">
                    <p className="font-extrabold text-gray-900 truncate">
                      {member.displayName || member.email}
                      {member.isYou && <span className="ml-2 text-[10px] font-bold text-ink-3">(you)</span>}
                    </p>
                    <p className="text-xs text-ink-3 truncate">{member.email}</p>
                    <p className="text-xs font-semibold text-gray-700 truncate">{member.title || (member.isMaster ? 'Master admin' : 'Staff')}</p>
                  </div>
                </div>

                <div className="flex-1 flex flex-wrap gap-1.5">
                  {member.isMaster ? (
                    <span className="text-[11px] font-bold px-2.5 py-1 rounded-full bg-brand-red-light text-brand-red">
                      Master admin: full access and team management
                    </span>
                  ) : ADMIN_AREAS.map((area) => {
                    const level = member.access?.[area.key] ?? 'none';
                    return (
                      <span key={area.key} className={clsx('text-[11px] font-bold px-2.5 py-1 rounded-full', levelChip[level])}>
                        {area.label}: {LEVELS.find((l) => l.value === level)?.label}
                      </span>
                    );
                  })}
                </div>

                <div className="flex items-center gap-3 lg:justify-end flex-shrink-0">
                  <div className="text-[11px] text-ink-3 text-right hidden sm:block">
                    <p>{member.active ? 'Active' : 'Suspended'}</p>
                    <p>Last sign-in {formatDate(member.lastLoginAt)}</p>
                  </div>
                  <IconButton label="Edit role and access" onClick={() => setEditing(member)} disabled={busyId === member.id}><Pencil size={15} /></IconButton>
                  <IconButton label="Create a new temporary password" onClick={() => resetPassword(member)} disabled={busyId === member.id}><KeyRound size={15} /></IconButton>
                  {!member.isYou && (
                    <IconButton
                      label={member.active ? 'Suspend access' : 'Restore access'}
                      onClick={() => toggleActive(member)}
                      disabled={busyId === member.id}
                      danger={member.active}
                    >
                      {member.active ? <UserX size={15} /> : <UserCheck size={15} />}
                    </IconButton>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="card p-5">
        <h2 className="text-sm font-black text-gray-900">What each level means</h2>
        <ul className="mt-2 space-y-1 text-sm text-ink-2">
          <li><strong>View</strong> lets someone open an area and read everything in it, but not change anything.</li>
          <li><strong>Manage</strong> also lets them approve, edit and act on things in that area.</li>
          <li><strong>Master admin</strong> can do everything, including adding people here and changing their access.</li>
          <li>Changes apply on the person's next action. Everyone can see the dashboard and their own notifications.</li>
        </ul>
      </div>

      {editing && (
        <MemberModal
          member={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={(created) => {
            setEditing(null);
            if (created) setPassword(created);
            load();
          }}
        />
      )}

      {password && <PasswordModal {...password} onClose={() => setPassword(null)} />}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="card p-4">
      <p className="text-2xl font-black text-gray-900">{value}</p>
      <p className="text-xs text-ink-3">{label}</p>
    </div>
  );
}

function IconButton({ label, onClick, disabled, danger, children }: {
  label: string; onClick: () => void; disabled?: boolean; danger?: boolean; children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      onClick={onClick}
      disabled={disabled}
      className={clsx(
        'w-9 h-9 rounded-xl border flex items-center justify-center transition-colors disabled:opacity-40',
        danger ? 'border-red-100 text-red-600 hover:bg-red-50' : 'border-brand-blue-light text-ink-2 hover:bg-brand-blue-light/60 hover:text-brand-blue'
      )}
    >
      {children}
    </button>
  );
}

function MemberModal({ member, onClose, onSaved }: {
  member: TeamMember | null;
  onClose: () => void;
  /** Receives the one-time password when the server generated one. */
  onSaved: (created: { name: string; email: string; value: string } | null) => void;
}) {
  const isNew = member === null;
  const [displayName, setDisplayName] = useState(member?.displayName ?? '');
  const [email, setEmail] = useState(member?.email ?? '');
  const [title, setTitle] = useState(member?.title ?? '');
  const [password, setPassword] = useState('');
  const [isMaster, setIsMaster] = useState(member?.isMaster ?? false);
  const [access, setAccess] = useState<Record<AdminArea, AccessLevel>>(fullAccessMap(member?.access ?? {}));
  const [busy, setBusy] = useState(false);

  // The preset that matches the current choices, if any; editing an area turns it into "Custom".
  const preset = ROLE_PRESETS.find((p) =>
    p.isMaster === isMaster &&
    (isMaster || ADMIN_AREAS.every((a) => (p.access[a.key] ?? 'none') === access[a.key])))?.key ?? 'custom';

  const applyPreset = (key: string) => {
    const chosen = ROLE_PRESETS.find((p) => p.key === key);
    if (!chosen) return;
    // Fill in the job title too, unless someone already typed their own.
    const previous = ROLE_PRESETS.find((p) => p.key === preset)?.label;
    if (!title.trim() || title === previous) setTitle(chosen.label);
    setIsMaster(chosen.isMaster);
    setAccess(fullAccessMap(chosen.access));
  };

  const save = async () => {
    if (!displayName.trim()) { toast.error("Enter the person's name"); return; }
    if (isNew && !/^\S+@\S+\.\S+$/.test(email.trim())) { toast.error('Enter a valid email address'); return; }
    if (isNew && password && password.length < 8) { toast.error('The password must be at least 8 characters'); return; }
    if (!isMaster && ADMIN_AREAS.every((a) => access[a.key] === 'none')) {
      if (!window.confirm('This person will only see the dashboard and their notifications. Continue?')) return;
    }

    setBusy(true);
    const accessInput = { isMaster, areas: isMaster ? {} : access };
    try {
      if (isNew) {
        const res = await adminTeamApi.create({
          email: email.trim(), displayName: displayName.trim(), title: title.trim() || undefined,
          password: password || undefined, access: accessInput,
        });
        toast.success(res.message ?? 'Team member added');
        onSaved(res.temporaryPassword
          ? { name: displayName.trim(), email: email.trim(), value: res.temporaryPassword }
          : null);
      } else {
        await adminTeamApi.update(member.id, { displayName: displayName.trim(), title: title.trim(), access: accessInput });
        toast.success('Team member updated');
        onSaved(null);
      }
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={isNew ? 'Add team member' : `Edit ${member.displayName || member.email}`} onClose={onClose} size="lg">
      <div className="p-6 space-y-5">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="label" htmlFor="staff-name">Full name</label>
            <input id="staff-name" className="input" value={displayName} onChange={(e) => setDisplayName(e.target.value)} maxLength={120} />
          </div>
          <div>
            <label className="label" htmlFor="staff-email">Email (used to sign in)</label>
            <input id="staff-email" className="input" type="email" value={email} onChange={(e) => setEmail(e.target.value)} disabled={!isNew} />
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="label" htmlFor="staff-role">Role</label>
            <select id="staff-role" className="input" value={preset} onChange={(e) => applyPreset(e.target.value)}>
              {ROLE_PRESETS.map((p) => <option key={p.key} value={p.key}>{p.label}</option>)}
              <option value="custom" disabled>Custom</option>
            </select>
          </div>
          <div>
            <label className="label" htmlFor="staff-title">Job title</label>
            <input id="staff-title" className="input" value={title} onChange={(e) => setTitle(e.target.value)} maxLength={80} placeholder="e.g. Finance officer" />
          </div>
        </div>

        {isMaster ? (
          <div className="rounded-2xl bg-brand-red-light px-4 py-3 text-sm text-brand-red font-semibold">
            A master admin can do everything in the admin console, including adding team members and changing their access.
          </div>
        ) : (
          <div className="rounded-2xl border border-brand-blue-light divide-y divide-brand-blue-light">
            {ADMIN_AREAS.map((area) => (
              <div key={area.key} className="p-3 sm:p-4 flex flex-col sm:flex-row sm:items-center gap-3">
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-extrabold text-gray-900">{area.label}</p>
                  <p className="text-xs text-ink-3">{area.description}</p>
                </div>
                <div className="inline-flex rounded-xl bg-gray-100 p-1 flex-shrink-0" role="radiogroup" aria-label={`${area.label} access`}>
                  {LEVELS.map((level) => (
                    <button
                      key={level.value}
                      type="button"
                      role="radio"
                      aria-checked={access[area.key] === level.value}
                      onClick={() => setAccess((current) => ({ ...current, [area.key]: level.value }))}
                      className={clsx(
                        'px-3 h-8 rounded-lg text-xs font-bold transition-colors',
                        access[area.key] === level.value ? 'bg-white text-brand-blue shadow-sm' : 'text-ink-3 hover:text-gray-700'
                      )}
                    >
                      {level.label}
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}

        {isNew && (
          <div>
            <label className="label" htmlFor="staff-password">Starting password</label>
            <input
              id="staff-password" className="input" type="text" autoComplete="new-password"
              value={password} onChange={(e) => setPassword(e.target.value)}
              placeholder="Leave blank to create one automatically"
            />
            <p className="mt-1 text-xs text-ink-3">Share it with the person privately. They can change it with "Forgot password" on the sign-in page.</p>
          </div>
        )}

        <div className="flex justify-end gap-2 pt-1">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn-primary flex items-center gap-2" disabled={busy} onClick={save}>
            {busy && <Loader2 size={14} className="animate-spin" />}
            {isNew ? 'Add team member' : 'Save changes'}
          </button>
        </div>
      </div>
    </Modal>
  );
}

function PasswordModal({ name, email, value, onClose }: { name: string; email: string; value: string; onClose: () => void }) {
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      toast.success('Password copied');
    } catch {
      toast.error('Copy failed. Select the password and copy it manually.');
    }
  };

  return (
    <Modal title="Temporary password" onClose={onClose} size="sm">
      <div className="p-6 space-y-4">
        <p className="text-sm text-ink-2">
          Give this to <strong>{name}</strong> privately. They sign in at the Admin tab with <strong>{email}</strong>.
          It won't be shown again.
        </p>
        <div className="flex items-center gap-2">
          <code className="flex-1 rounded-xl bg-gray-100 px-3 py-2.5 font-mono text-base font-bold text-gray-900 select-all break-all">{value}</code>
          <button type="button" className="btn-secondary flex items-center gap-1.5" onClick={copy}><Copy size={14} /> Copy</button>
        </div>
        <div className="flex justify-end">
          <button className="btn-primary" onClick={onClose}>Done</button>
        </div>
      </div>
    </Modal>
  );
}

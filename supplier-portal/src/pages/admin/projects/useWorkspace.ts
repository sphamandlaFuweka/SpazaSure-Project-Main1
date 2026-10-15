import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { adminProjectsApi } from '../../../services/api';
import { starterPhase1, starterPhase2 } from './starterData';
import { emptyWorkspace, type Kind, type KindMap, type RawRecord, type Saved, type Workspace } from './types';

const toSaved = <K extends Kind>(r: RawRecord) =>
  ({ ...(r.data as object), id: r.id, createdAt: r.createdAt, updatedAt: r.updatedAt }) as Saved<KindMap[K]>;

const stripMeta = <T extends object>(rec: T) => {
  const { id: _id, createdAt: _c, updatedAt: _u, ...data } = rec as T & { id?: string; createdAt?: string; updatedAt?: string | null };
  return data;
};

function group(rows: RawRecord[]): Workspace {
  const ws = emptyWorkspace();
  rows.forEach((r) => { if (r.kind in ws) (ws[r.kind] as Saved<object>[]).push(toSaved(r)); });
  return ws;
}

export interface WorkspaceApi {
  ws: Workspace;
  loading: boolean;
  error: boolean;
  importing: boolean;
  reload: () => Promise<void>;
  create: <K extends Kind>(kind: K, data: KindMap[K]) => Promise<Saved<KindMap[K]> | null>;
  update: <K extends Kind>(kind: K, rec: Saved<KindMap[K]>, patch: Partial<KindMap[K]>) => Promise<boolean>;
  remove: (kind: Kind, id: string) => Promise<boolean>;
  importStarter: () => Promise<void>;
}

export function useWorkspace(): WorkspaceApi {
  const [ws, setWs] = useState<Workspace>(emptyWorkspace);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [importing, setImporting] = useState(false);

  const reload = useCallback(async () => {
    setLoading(true); setError(false);
    try { setWs(group(await adminProjectsApi.list())); } catch { setError(true); } finally { setLoading(false); }
  }, []);
  useEffect(() => { reload(); }, [reload]);

  const create = useCallback(async <K extends Kind>(kind: K, data: KindMap[K]) => {
    try {
      const saved = toSaved<K>(await adminProjectsApi.create(kind, data));
      setWs((w) => ({ ...w, [kind]: [...w[kind], saved] }));
      return saved;
    } catch { return null; }
  }, []);

  // Optimistic: the change shows at once and is rolled back if the server refuses it.
  const update = useCallback(async <K extends Kind>(kind: K, rec: Saved<KindMap[K]>, patch: Partial<KindMap[K]>) => {
    const next = { ...rec, ...patch } as Saved<KindMap[K]>;
    const swap = (to: Saved<KindMap[K]>) => setWs((w) => ({ ...w, [kind]: (w[kind] as Saved<KindMap[K]>[]).map((x) => (x.id === rec.id ? to : x)) }));
    swap(next);
    try { await adminProjectsApi.update(rec.id, stripMeta(next)); return true; } catch { swap(rec); return false; }
  }, []);

  const remove = useCallback(async (kind: Kind, id: string) => {
    try {
      await adminProjectsApi.remove(id);
      setWs((w) => ({ ...w, [kind]: (w[kind] as Saved<object>[]).filter((x) => x.id !== id) }));
      return true;
    } catch { return false; }
  }, []);

  const importStarter = useCallback(async () => {
    setImporting(true);
    try {
      const p1 = starterPhase1();
      const saved1 = await adminProjectsApi.createMany(p1.batch);
      const people = saved1.filter((r) => r.kind === 'person');
      const projects = saved1.filter((r) => r.kind === 'project');
      const personIds = Object.fromEntries(p1.personKeys.map((k, i) => [k, people[i].id])) as Parameters<typeof starterPhase2>[0];
      const projectIds = Object.fromEntries(p1.projectKeys.map((k, i) => [k, projects[i].id])) as Parameters<typeof starterPhase2>[1];
      const p2 = starterPhase2(personIds, projectIds);
      await adminProjectsApi.createMany(p2.batch);
      await Promise.all(projects.map((r) => adminProjectsApi.update(r.id, { ...r.data, lead: p2.projectLeads[r.id] })));
      toast.success('Workbook data imported');
      await reload();
    } catch {
      toast.error('The import stopped part-way. Reload the page before trying again.');
    } finally { setImporting(false); }
  }, [reload]);

  return { ws, loading, error, importing, reload, create, update, remove, importStarter };
}

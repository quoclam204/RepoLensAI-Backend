import { User, UserRole } from "./models";

export class UserService {
    private users: User[] = [];

    getUser(id: string): User | undefined {
        return this.users.find(u => u.id === id);
    }

    addUser(user: User): void {
        this.users.push(user);
    }

    listAdmins(): User[] {
        return this.users.filter((u): boolean => u.role === ("admin" as UserRole));
    }
}

export function formatUser(user: User): string {
    return `${user.name} (${user.id})`;
}

export type ServiceResult = { ok: boolean };

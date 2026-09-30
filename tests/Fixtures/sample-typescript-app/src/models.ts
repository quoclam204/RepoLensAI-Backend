export interface User {
    id: string;
    name: string;
    role?: UserRole;
}

export type UserRole = "admin" | "user";

export enum UserStatus {
    Active = "active",
    Inactive = "inactive"
}

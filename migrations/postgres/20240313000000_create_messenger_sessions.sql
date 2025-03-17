- +goose Up
-- +goose StatementBegin

CREATE TABLE IF NOT EXISTS messenger_sessions (
    id UUID PRIMARY KEY,
    user_id VARCHAR(255) NOT NULL,
    messenger_type VARCHAR(50) NOT NULL,
    session_data TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    last_activity_at TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT false,
    CONSTRAINT fk_user_id FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE INDEX idx_messenger_sessions_user_id ON messenger_sessions(user_id);
CREATE INDEX idx_messenger_sessions_messenger_type ON messenger_sessions(messenger_type);

-- +goose StatementEnd

-- +goose Down
-- +goose StatementBegin

DROP TABLE IF EXISTS messenger_sessions;

-- +goose StatementEnd 
package com.bytetum.flink;

import org.apache.flink.configuration.Configuration;
import org.apache.flink.streaming.api.environment.StreamExecutionEnvironment;
import org.apache.flink.table.api.EnvironmentSettings;
import org.apache.flink.table.api.StatementSet;
import org.apache.flink.table.api.TableResult;
import org.apache.flink.table.api.bridge.java.StreamTableEnvironment;
import org.apache.flink.table.api.internal.TableEnvironmentInternal;
import org.apache.flink.table.operations.Operation;
import org.apache.flink.table.operations.command.ResetOperation;
import org.apache.flink.table.operations.command.SetOperation;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Executes one local Flink SQL script as an application-mode job.
 *
 * <p>The Flink SQL client has a script parser, but it is a separate process and
 * is not the application-mode entry point used by the operator. This small
 * runner keeps the same useful semantics: statements are parsed one at a time,
 * DDL/configuration is applied immediately, and all INSERT statements are
 * submitted together through one StatementSet.</p>
 */
public final class SqlApp {

    private SqlApp() {
    }

    public static void main(String[] args) throws Exception {
        if (args == null || args.length != 1 || args[0] == null || args[0].isBlank()) {
            System.err.println("Usage: flink run -c com.bytetum.flink.SqlApp <local-sql-file>");
            System.exit(2);
            return;
        }

        Path sqlFile = Path.of(args[0]);
        if (!Files.isRegularFile(sqlFile)) {
            throw new IOException("SQL file does not exist or is not a regular file: " + sqlFile);
        }

        String script = Files.readString(sqlFile, StandardCharsets.UTF_8);
        StreamExecutionEnvironment executionEnvironment =
                StreamExecutionEnvironment.getExecutionEnvironment();
        StreamTableEnvironment tableEnvironment = StreamTableEnvironment.create(
                executionEnvironment,
                EnvironmentSettings.newInstance().inStreamingMode().build());
        StatementSet statementSet = tableEnvironment.createStatementSet();
        TableEnvironmentInternal internalEnvironment = (TableEnvironmentInternal) tableEnvironment;
        Configuration configuration = tableEnvironment.getConfig().getConfiguration();
        Configuration initialConfiguration = new Configuration(configuration);

        int insertCount = 0;
        for (String statement : splitStatements(script)) {
            String sql = statement.trim();
            if (sql.startsWith("\uFEFF")) {
                sql = sql.substring(1).trim();
            }
            if (sql.isEmpty() || isStatementSetMarker(sql)) {
                continue;
            }

            if (isInsert(sql)) {
                statementSet.addInsertSql(sql);
                insertCount++;
            } else {
                Operation operation = internalEnvironment.getParser().parse(sql).get(0);
                // SET/RESET are SQL-client commands, not executable Table API operations.
                if (operation instanceof SetOperation set) {
                    if (set.getKey().isEmpty() || set.getValue().isEmpty()) {
                        throw new IllegalArgumentException(
                                "SET requires a key and value in a non-interactive SQL script");
                    }
                    configuration.setString(set.getKey().get(), set.getValue().get());
                    continue;
                }
                if (operation instanceof ResetOperation reset) {
                    if (reset.getKey().isPresent()) {
                        String key = reset.getKey().get();
                        configuration.removeKey(key);
                        if (initialConfiguration.containsKey(key)) {
                            configuration.setString(key, initialConfiguration.getString(key, null));
                        }
                    } else {
                        for (String key : new ArrayList<>(configuration.keySet())) {
                            configuration.removeKey(key);
                        }
                        configuration.addAll(initialConfiguration);
                    }
                    continue;
                }
                // DDL/catalog changes precede INSERT compilation. CTAS jobs are awaited.
                TableResult result = internalEnvironment.executeInternal(operation);
                if (result.getJobClient().isPresent()) {
                    result.await();
                }
            }
        }

        if (insertCount > 0) {
            // Streaming jobs normally do not terminate; awaiting here keeps the
            // application process alive and propagates a failed job to Flink.
            statementSet.execute().await();
        }
    }

    private static boolean isInsert(String sql) {
        String[] words = sql.toUpperCase(Locale.ROOT).trim().split("\\s+", 3);
        return words.length >= 2
                && words[0].equals("INSERT")
                && (words[1].equals("INTO") || words[1].equals("OVERWRITE"));
    }

    private static boolean isStatementSetMarker(String sql) {
        String upper = sql.toUpperCase(Locale.ROOT).replaceAll("\\s+", " ").trim();
        return upper.equals("BEGIN STATEMENT SET")
                || upper.equals("END")
                || upper.equals("END STATEMENT SET");
    }

    /**
     * Splits a script on SQL statement terminators while respecting quoted
     * strings/identifiers and both SQL comment forms. Semicolons in a Kafka
     * property, JSON value, or string literal must not create a new statement.
     * Comments are replaced by whitespace so a comment cannot accidentally
     * become part of a neighbouring statement.
     */
    static List<String> splitStatements(String script) {
        List<String> statements = new ArrayList<>();
        StringBuilder current = new StringBuilder();
        boolean singleQuote = false;
        boolean doubleQuote = false;
        boolean backtickQuote = false;
        boolean lineComment = false;
        boolean blockComment = false;

        for (int i = 0; i < script.length(); i++) {
            char c = script.charAt(i);
            char next = i + 1 < script.length() ? script.charAt(i + 1) : '\0';

            if (lineComment) {
                if (c == '\n' || c == '\r') {
                    lineComment = false;
                    current.append(c);
                } else {
                    current.append(' ');
                }
                continue;
            }
            if (blockComment) {
                if (c == '*' && next == '/') {
                    current.append("  ");
                    i++;
                    blockComment = false;
                } else {
                    current.append(c == '\n' || c == '\r' ? c : ' ');
                }
                continue;
            }

            if (!singleQuote && !doubleQuote && !backtickQuote && c == '-' && next == '-') {
                current.append("  ");
                i++;
                lineComment = true;
                continue;
            }
            if (!singleQuote && !doubleQuote && !backtickQuote && c == '/' && next == '*') {
                current.append("  ");
                i++;
                blockComment = true;
                continue;
            }

            if (singleQuote) {
                current.append(c);
                if (c == '\'' && next == '\'') {
                    current.append(next);
                    i++;
                } else if (c == '\'') {
                    singleQuote = false;
                }
                continue;
            }
            if (doubleQuote) {
                current.append(c);
                if (c == '"' && next == '"') {
                    current.append(next);
                    i++;
                } else if (c == '"') {
                    doubleQuote = false;
                }
                continue;
            }
            if (backtickQuote) {
                current.append(c);
                if (c == '`' && next == '`') {
                    current.append(next);
                    i++;
                } else if (c == '`') {
                    backtickQuote = false;
                }
                continue;
            }

            if (c == '\'') {
                singleQuote = true;
            } else if (c == '"') {
                doubleQuote = true;
            } else if (c == '`') {
                backtickQuote = true;
            } else if (c == ';') {
                statements.add(current.toString());
                current.setLength(0);
                continue;
            }
            current.append(c);
        }

        if (singleQuote || doubleQuote || backtickQuote || blockComment) {
            throw new IllegalArgumentException("Unterminated quote or block comment in SQL script");
        }
        if (!current.toString().trim().isEmpty()) {
            statements.add(current.toString());
        }
        return statements;
    }
}
